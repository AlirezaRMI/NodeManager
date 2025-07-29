using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Application.Services.Interfaces;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace Application.Services.implementations;

public sealed class DockerService : IDockerService
{
    private readonly IDockerClient _docker;
    private readonly ILogger<DockerService> _log;

    public DockerService(IDockerClient dockerClient, ILogger<DockerService> logger)
    {
        _docker = dockerClient;
        _log    = logger;
    }

    

    public async Task<string> CreateContainerAsync(
        string                    imageName,
        string                    containerName,
        List<string>              portMappings,
        Dictionary<string,string> environmentVariables,
        List<string>              volumeMappings,
        string?                   command     = null,
        string                    networkMode = "bridge")
    {
        await EnsureImageAsync(imageName);
        await RemoveExistingContainerIfAny(containerName);
        
        var portBindings = new Dictionary<string, IList<PortBinding>>();
        var exposedPorts = new Dictionary<string, EmptyStruct>();

        foreach (var map in portMappings)
        {
            var parts         = map.Split(':');
            var hostPort      = parts[0];
            var contPortProto = parts[1];

            if (!portBindings.TryGetValue(contPortProto, out var lst))
                portBindings[contPortProto] = lst = new List<PortBinding>();

            lst.Add(new PortBinding { HostPort = hostPort });
            exposedPorts[contPortProto] = default;          
        }

        var create = new CreateContainerParameters
        {
            Image        = imageName,
            Name         = containerName,
            Tty          = false,
            Env          = environmentVariables.Select(kv => $"{kv.Key}={kv.Value}").ToList(),
            Cmd          = command?.Split(' '),
            ExposedPorts = exposedPorts,
            HostConfig   = new HostConfig
            {
                NetworkMode  = networkMode,
                PortBindings = portBindings,
                Mounts       = volumeMappings.Select(v =>
                {
                    var s = v.Split(':');  
                    return new Mount
                    {
                        Type     = "bind",
                        Source   = s[0],
                        Target   = s[1],
                        ReadOnly = s.Length > 2 && s[2].Contains("ro")
                    };
                }).ToList()
            }
        };

        _log.LogInformation("Creating container {Name} ({Image}) ...", containerName, imageName);
        var resp = await _docker.Containers.CreateContainerAsync(create);
        return resp.ID;
    }

    public Task StartContainerAsync (string id) => _docker.Containers.StartContainerAsync(id,new());
    public Task StopContainerAsync  (string id) => _docker.Containers.StopContainerAsync (id,new());
    public Task DeleteContainerAsync(string id) => _docker.Containers.RemoveContainerAsync(id,new(){Force=true});

    public async Task<string> GetContainerStatusAsync(string id)
        => (await _docker.Containers.InspectContainerAsync(id)).State?.Status ?? "unknown";

    

    public async Task<string> GetContainerLogsAsync(string id)
    {
        var p = new ContainerLogsParameters { ShowStdout = true, ShowStderr = true };
        await using var stream = await _docker.Containers.GetContainerLogsAsync(id, p, CancellationToken.None);
        using var mux = new MultiplexedStream(stream, true);

        var (outBuf, errBuf) = await DemuxAsync(mux);
        return outBuf + (errBuf.Length == 0 ? "" : "\n---stderr---\n" + errBuf);
    }

    public async Task<string> ExecuteCommandInContainerAsync(string id, string[] cmd)
    {
        var exec = await _docker.Exec.ExecCreateContainerAsync(id,
                     new() { Cmd = cmd, AttachStdout = true, AttachStderr = true });

        using var stream = await _docker.Exec.StartAndAttachContainerExecAsync(exec.ID, false);
        var (stdout, _)  = await DemuxAsync(stream);
        return stdout.Trim();
    }

    public Task PauseContainerAsync  (string id) => _docker.Containers.PauseContainerAsync  (id);
    public Task UnpauseContainerAsync(string id) => _docker.Containers.UnpauseContainerAsync(id);

    public Task CreateDirectoryOnHostAsync(string path)
        => Shell("mkdir", $"-p {path}");

    public async Task WriteFileOnHostAsync(string filePath, string content)
    {
        var tmp = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmp, content);
        try     { await Shell("bash", $"-c \"cat '{tmp}' > '{filePath}'\""); }
        finally { File.Delete(tmp); }
    }

    public Task OpenFirewallPortAsync (int p,string proto="tcp")
        => Shell("ufw",$"allow {p}/{proto}", ignoreExists:true);

    public Task CloseFirewallPortAsync(int p,string proto="tcp")
        => Shell("ufw",$"delete allow {p}/{proto}", ignoreExists:true);
    

    private async Task EnsureImageAsync(string image)
    {
        var images = await _docker.Images.ListImagesAsync(new ImagesListParameters
        {
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["reference"] = new Dictionary<string, bool> { [image] = default }
            }
        });
        

        if (images.Count == 0)
        {
            _log.LogInformation("Pulling Docker image {Image} ...", image);
            await _docker.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = image, Tag = "latest" },
                null,
                new Progress<JSONMessage>());
        }
    }

    private async Task RemoveExistingContainerIfAny(string name)
    {
        var containers = await _docker.Containers.ListContainersAsync(new()
        {
            All     = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["name"] = new Dictionary<string, bool> { [name] = default }
            }
        });

        foreach (var c in containers)
        {
            _log.LogWarning("Removing existing container {Name} ({Id})", name, c.ID);
            await _docker.Containers.RemoveContainerAsync(c.ID, new() { Force = true });
        }
    }
    
    private static async Task<(string stdout,string stderr)> DemuxAsync(MultiplexedStream stream)
    {
        var outSb = new StringBuilder();
        var errSb = new StringBuilder();
        var buf   = ArrayPool<byte>.Shared.Rent(81920);

        try
        {
            while (true)
            {
                var res = await stream.ReadOutputAsync(buf, 0, buf.Length, CancellationToken.None);
                if (res.EOF) break;

                var text = Encoding.UTF8.GetString(buf, 0, res.Count);
                if (res.Target == MultiplexedStream.TargetStream.StandardOut)
                    outSb.Append(text);
                else
                    errSb.Append(text);
            }
        }
        finally { ArrayPool<byte>.Shared.Return(buf); }

        return (outSb.ToString(), errSb.ToString());
    }

    private async Task Shell(string cmd, string args, bool ignoreExists=false)
    {
        var psi = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new ProcessStartInfo("cmd.exe", $"/c {cmd} {args}")
            : new ProcessStartInfo("sudo",      $"{cmd} {args}");

        psi.RedirectStandardError  = true;
        psi.RedirectStandardOutput = true;
        psi.CreateNoWindow         = true;
        psi.UseShellExecute        = false;

        using var p = Process.Start(psi)!;
        var err = await p.StandardError.ReadToEndAsync();
        await p.StandardOutput.ReadToEndAsync();
        await p.WaitForExitAsync();

        if (p.ExitCode != 0 && !(ignoreExists && err.Contains("already exists")))
            throw new InvalidOperationException($"Host command '{cmd} {args}' failed → {err}");
    }
}
