using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Application.Services.Interfaces;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace Application.Services.implementations;

public sealed class DockerService(IDockerClient client, ILogger<IDockerService> logger) : IDockerService
{
    public async Task<string> CreateContainerAsync(
        string imageName, string containerName, List<string> portMappings,
        Dictionary<string, string> environmentVariables, List<string> volumeMappings,
        string? command = null, string? networkMode = null)
    {
        await EnsureImageAsync(imageName);
        await RemoveExistingContainerIfAny(containerName);

        var portBindings = new Dictionary<string, IList<PortBinding>>();
        var exposedPorts = new Dictionary<string, EmptyStruct>();

        foreach (var map in portMappings)
        {
            var parts = map.Split(':');
            var hostPort = parts[0];
            var contPortProto = parts[1];
            if (!portBindings.TryGetValue(contPortProto, out var lst))
                portBindings[contPortProto] = lst = new List<PortBinding>();
            lst.Add(new PortBinding { HostPort = hostPort });
            exposedPorts[contPortProto] = default;
        }

        var hostConfig = new HostConfig
        {
            PortBindings = portBindings,
            Mounts = volumeMappings.Select(v =>
            {
                var s = v.Split(':');
                return new Mount
                    { Type = "bind", Source = s[0], Target = s[1], ReadOnly = s.Length > 2 && s[2].Contains("ro") };
            }).ToList(),
            RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.Always }
        };

        if (!string.IsNullOrEmpty(networkMode))
        {
            hostConfig.NetworkMode = networkMode;
        }

        var create = new CreateContainerParameters
        {
            Image = imageName, Name = containerName, Tty = false,
            Env = environmentVariables.Select(kv => $"{kv.Key}={kv.Value}").ToList(),
            Cmd = command?.Split(' '), ExposedPorts = exposedPorts, HostConfig = hostConfig,
        };

        logger.LogInformation("Creating container {Name} ({Image}) ...", containerName, imageName);
        var resp = await client.Containers.CreateContainerAsync(create);
        return resp.ID;
    }

    public Task StartContainerAsync(string id) =>
        client.Containers.StartContainerAsync(id, new ContainerStartParameters());

    public Task StopContainerAsync(string id) =>
        client.Containers.StopContainerAsync(id, new ContainerStopParameters());

    public Task DeleteContainerAsync(string id) =>
        client.Containers.RemoveContainerAsync(id, new ContainerRemoveParameters { Force = true });

    public async Task<string> GetContainerStatusAsync(string id) =>
        (await client.Containers.InspectContainerAsync(id)).State?.Status ?? "unknown";

    public async Task<string> GetContainerLogsAsync(string id)
    {
        var p = new ContainerLogsParameters { ShowStdout = true, ShowStderr = true };
        await using var stream = await client.Containers.GetContainerLogsAsync(id, p, CancellationToken.None);
        using var mux = new MultiplexedStream(stream, true);
        var (outBuf, errBuf) = await DemuxAsync(mux);
        return outBuf + (errBuf.Length == 0 ? "" : "\n---stderr---\n" + errBuf);
    }
    
    public async Task<ContainerStatsResponse> GetContainerStatsAsync(string containerId)
    {
        var cts = new CancellationTokenSource();
        ContainerStatsResponse? statsResponse = null;

        var progress = new Progress<ContainerStatsResponse>(stats =>
        {
            if (stats.MemoryStats == null) return;
            statsResponse = stats;
            cts.Cancel();
        });

        var parameters = new ContainerStatsParameters { Stream = true };
        
        try
        {
            await client.Containers.GetContainerStatsAsync(containerId, parameters, progress, cts.Token);
        }
        catch (TaskCanceledException)
        {
           
        }
        
        if (statsResponse == null)
            throw new InvalidOperationException($"Could not retrieve stats for container {containerId}.");
            
        return statsResponse;
    }

    public async Task<string> ExecuteCommandInContainerAsync(string id, string[] cmd)
    {
        var exec = await client.Exec.ExecCreateContainerAsync(id,
            new ContainerExecCreateParameters { Cmd = cmd, AttachStdout = true, AttachStderr = true });
        using var stream = await client.Exec.StartAndAttachContainerExecAsync(exec.ID, false);
        var (stdout, _) = await DemuxAsync(stream);
        return stdout.Trim();
    }

    public Task PauseContainerAsync(string id) => client.Containers.PauseContainerAsync(id);
    public Task UnpauseContainerAsync(string id) => client.Containers.UnpauseContainerAsync(id);
    public Task CreateDirectoryOnHostAsync(string path) => Shell("mkdir", $"-p {path}");

    public async Task WriteFileOnHostAsync(string filePath, string content)
    {
        var tmp = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmp, content);
        try
        {
            await Shell("bash", $"-c \"cat '{tmp}' > '{filePath}'\"");
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    public Task OpenFirewallPortAsync(int p, string proto = "tcp") =>
        Shell("ufw", $"allow {p}/{proto}", ignoreExists: true);

    public async Task<string> ExecuteCommandOnHostAsync(string command, string args)
    {
        var psi = new ProcessStartInfo("sudo", $"{command} {args}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi)!;
        var stdout = await p.StandardOutput.ReadToEndAsync();
        var stderr = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();

        if (p.ExitCode != 0)
        {
            throw new InvalidOperationException($"Host command '{command} {args}' failed → {stderr}");
        }

        return stdout;
    }


    public Task CloseFirewallPortAsync(int p, string proto = "tcp") =>
        Shell("ufw", $"delete allow {p}/{proto}", ignoreExists: true);

    public async Task AddTrafficCountingRuleAsync(int port)
    {
        if (!await ChainExistsAsync("EASYHUB_TRAFFIC"))
        {
            logger.LogInformation("Creating EASYHUB_TRAFFIC chain in iptables...");
            await Shell("iptables", "-N EASYHUB_TRAFFIC");
            await Shell("iptables", "-A FORWARD -j EASYHUB_TRAFFIC");
        }

        await Shell("iptables", $"-A EASYHUB_TRAFFIC -p tcp --dport {port}");
        await Shell("iptables", $"-A EASYHUB_TRAFFIC -p tcp --sport {port}");
        await Shell("netfilter-persistent", "save");
    }

    public async Task RemoveTrafficCountingRuleAsync(int port)
    {
        if (await ChainExistsAsync("EASYHUB_TRAFFIC"))
        {
            await Shell("iptables", $"-D EASYHUB_TRAFFIC -p tcp --dport {port}", ignoreExists: true);
            await Shell("iptables", $"-D EASYHUB_TRAFFIC -p tcp --sport {port}", ignoreExists: true);
            await Shell("iptables-save", "> /etc/iptables/rules.v4");
        }
    }

    private async Task EnsureImageAsync(string image)
    {
        var images = await client.Images.ListImagesAsync(new ImagesListParameters
        {
            Filters = new Dictionary<string, IDictionary<string, bool>>
                { ["reference"] = new Dictionary<string, bool> { [image] = default } }
        });
        if (images.Count == 0)
        {
            logger.LogInformation("Pulling Docker image {Image} ...", image);
            await client.Images.CreateImageAsync(new ImagesCreateParameters { FromImage = image, Tag = "latest" }, null,
                new Progress<JSONMessage>());
        }
    }

    private async Task RemoveExistingContainerIfAny(string name)
    {
        var containers = await client.Containers.ListContainersAsync(new()
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>
                { ["name"] = new Dictionary<string, bool> { [name] = default } }
        });
        foreach (var c in containers)
        {
            logger.LogWarning("Removing existing container {Name} ({Id})", name, c.ID);
            await client.Containers.RemoveContainerAsync(c.ID, new() { Force = true });
        }
    }

    private static async Task<(string stdout, string stderr)> DemuxAsync(MultiplexedStream stream)
    {
        var outSb = new StringBuilder();
        var errSb = new StringBuilder();
        var buf = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            while (true)
            {
                var res = await stream.ReadOutputAsync(buf, 0, buf.Length, CancellationToken.None);
                if (res.EOF) break;
                var text = Encoding.UTF8.GetString(buf, 0, res.Count);
                if (res.Target == MultiplexedStream.TargetStream.StandardOut) outSb.Append(text);
                else errSb.Append(text);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buf);
        }

        return (outSb.ToString(), errSb.ToString());
    }

    public async Task Shell(string cmd, string args, bool ignoreExists = false)
    {
        var psi = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new ProcessStartInfo("cmd.exe", $"/c {cmd} {args}")
            : new ProcessStartInfo("sudo", $"{cmd} {args}");
        psi.RedirectStandardError = true;
        psi.RedirectStandardOutput = true;
        psi.CreateNoWindow = true;
        psi.UseShellExecute = false;
        using var p = Process.Start(psi)!;
        var err = await p.StandardError.ReadToEndAsync();
        await p.StandardOutput.ReadToEndAsync();
        await p.WaitForExitAsync();
        if (p.ExitCode != 0 &&
            !(ignoreExists && (err.Contains("already exists") || err.Contains("No such file or directory"))))
            throw new InvalidOperationException($"Host command '{cmd} {args}' failed → {err}");
    }

    public async Task<bool> ChainExistsAsync(string chainName)
    {
        var psi = new ProcessStartInfo("sudo", $"iptables -n -L {chainName}")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        using var p = Process.Start(psi)!;
        await p.WaitForExitAsync();
        return p.ExitCode == 0;
    }
    
}