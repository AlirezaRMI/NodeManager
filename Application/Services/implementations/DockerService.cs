using System.Diagnostics;
using System.Runtime.InteropServices; 
using System.Text;
using Application.Services.Interfaces; 
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace Application.Services.implementations;

public class DockerService(IDockerClient dockerClient, ILogger<IDockerService> logger) : IDockerService
{
    public async Task<string> CreateContainerAsync(string imageName, string containerName, List<string> portMappings,
        Dictionary<string, string> environmentVariables, List<string> volumeMappings, string? command = null, string networkMode = "bridge")
    {
        var portBindings = new Dictionary<string, IList<PortBinding>>();

        foreach (var mapping in portMappings)
        {
            var split = mapping.Split(':');
            var hostPort = split[0];
            var containerPortProto = split[1];
            if (!portBindings.TryGetValue(containerPortProto,
                    out IList<PortBinding>? list))
            {
                list = new List<PortBinding>();
                portBindings[containerPortProto] = list;
            }
            list.Add(new PortBinding { HostPort = hostPort });
        }

        var createParams = new CreateContainerParameters
        {
            Image = imageName,
            Name = containerName,
            Tty = false,
            Env = environmentVariables.Select(kv => $"{kv.Key}={kv.Value}").ToList(),
            HostConfig = new HostConfig
            {
                PortBindings = portBindings,
                NetworkMode = networkMode,
                Mounts = volumeMappings.Select(v => new Mount
                {
                    Type = "bind",
                    Source = v.Split(':')[0],
                    Target = v.Split(':')[1]
                }).ToList()
            }
        };
        if (command != null) createParams.Cmd = command.Split(' ');
        
        logger.LogInformation("Attempting to create container '{ContainerName}' with image '{ImageName}'. Network: {NetworkMode}, Ports: {Ports}, Volumes: {Volumes}, Env: {EnvCount}",
            containerName, imageName, networkMode, string.Join(",", portMappings), string.Join(",", volumeMappings), environmentVariables.Count);

        try
        {
            var response = await dockerClient.Containers.CreateContainerAsync(createParams);
            logger.LogInformation("Container '{ContainerName}' created with ID: {ContainerId}", containerName, response.ID);
            return response.ID;
        }
        catch (DockerApiException dex)
        {
            logger.LogError(dex, "Docker API error creating container '{ContainerName}': {StatusCode} - {Message}", 
                            containerName, dex.StatusCode, dex.Message);
            throw new InvalidOperationException($"Docker API error: {dex.StatusCode} - {dex.Message}", dex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create container '{ContainerName}'.", containerName);
            throw;
        }
    }

    public async Task StartContainerAsync(string containerId) => await dockerClient.Containers.StartContainerAsync(containerId, new ContainerStartParameters());
    public async Task StopContainerAsync(string containerId) => await dockerClient.Containers.StopContainerAsync(containerId, new ContainerStopParameters());
    public async Task DeleteContainerAsync(string containerId) => await dockerClient.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = true });
    public async Task<string> GetContainerStatusAsync(string containerId) => (await dockerClient.Containers.InspectContainerAsync(containerId)).State.Status;


    [Obsolete("Use newer logging methods if possible.")]
    public async Task<string> GetContainerLogsAsync(string containerId)
    {
        var parameters = new ContainerLogsParameters { ShowStdout = true, ShowStderr = true, Timestamps = true };
        await using var stream = await dockerClient.Containers.GetContainerLogsAsync(containerId, parameters, CancellationToken.None);
        using var multiplexedStream = new MultiplexedStream(stream,true); 
        
        logger.LogDebug("Log stream is multiplexed. Demultiplexing...");
        var (stdout, stderr) = await DemultiplexStreamAsync(multiplexedStream, CancellationToken.None);
        return stdout + (string.IsNullOrEmpty(stderr) ? "" : $"\n--- STDERR ---\n{stderr}");
    
    }
    
    public async Task<string> ExecuteCommandInContainerAsync(string containerId, string[] command)
    {
        var execCreate = await dockerClient.Exec.ExecCreateContainerAsync(containerId, new ContainerExecCreateParameters { AttachStdout = true, AttachStderr = true, Cmd = command });
      
        using var stream = await dockerClient.Exec.StartAndAttachContainerExecAsync(execCreate.ID, false);
        var (stdout, stderr) = await DemultiplexStreamAsync(stream, CancellationToken.None);

        if (!string.IsNullOrEmpty(stderr))
        {
            logger.LogWarning("Command '{Command}' in container {ContainerId} returned Stderr: {Stderr}", string.Join(" ", command), containerId, stderr);
        }
        return stdout.Trim();
    }
    
    public async Task CreateDirectoryOnHostAsync(string path)
    {
        var result = await ExecuteShellCommandAsync("sudo", $"mkdir -p {path}");
        if (result.ExitCode != 0)
        {
            logger.LogError("Failed to create directory '{Path}' on host. Stderr: {StdErr}", path, result.StdErr);
            throw new InvalidOperationException($"Failed to create directory {path} on host: {result.StdErr}");
        }
    }

    public async Task OpenFirewallPortAsync(int port, string protocol = "tcp")
    {
        var result = await ExecuteShellCommandAsync("sudo", $"ufw allow {port}/{protocol}");
        if (result.ExitCode != 0 && !result.StdErr.Contains("Rule already exists"))
        {
            logger.LogWarning("Failed to open port {Port}/{Protocol} in firewall. Stderr: {StdErr}", port, protocol, result.StdErr);
            throw new InvalidOperationException($"Failed to open port {port}/{protocol} in firewall: {result.StdErr}");
        }
    }

    public async Task CloseFirewallPortAsync(int port, string protocol = "tcp")
    {
        var result = await ExecuteShellCommandAsync("sudo", $"ufw delete allow {port}/{protocol}");
        if (result.ExitCode != 0 && !result.StdErr.Contains("No matching rule"))
        {
            logger.LogWarning("Failed to close port {Port}/{Protocol} in firewall. Stderr: {StdErr}", port, protocol, result.StdErr);
            throw new InvalidOperationException($"Failed to close port {port}/{protocol} in firewall: {result.StdErr}");
        }
    }

    public async Task WriteFileOnHostAsync(string filePath, string content)
    {
        var tempFilePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFilePath, content); 
        try
        {
            var result = await ExecuteShellCommandAsync("sudo", $"bash -c \"cat {tempFilePath} > {filePath}\"");
            if (result.ExitCode != 0)
            {
                logger.LogError("Failed to write file '{FilePath}' on host. Stderr: {StdErr}", filePath, result.StdErr);
                throw new InvalidOperationException($"Failed to write file {filePath} on host: {result.StdErr}");
            }
        }
        finally
        {
            File.Delete(tempFilePath); 
        }
    }
    
    private async Task<(int ExitCode, string StdOut, string StdErr)> ExecuteShellCommandAsync(
        string command,
        string arguments)
    {
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        string shell = isWindows ? "cmd.exe" : "/bin/bash";
        string shellArgs = isWindows ? $"/c {command} {arguments}" : $"-c \"{command} {arguments}\"";
        
        var startInfo = new ProcessStartInfo
        {
            FileName = shell,
            Arguments = shellArgs,
            WorkingDirectory = isWindows ? Path.GetTempPath() : "/", 
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = new Process();
        process.StartInfo = startInfo;
        
        logger.LogDebug("Running host command: '{Shell} {ShellArgs}' in '{WorkingDirectory}'", shell, shellArgs, startInfo.WorkingDirectory);
        
        process.Start();

        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            logger.LogWarning("Host command '{Cmd} {Args}' failed with code {Code}. Stderr: {Err}. StdOut: {StdOut}", 
                              command, arguments, process.ExitCode, stderr, stdout);
        }
        return (process.ExitCode, stdout, stderr);
    }
    
 
    private static async Task<(string stdout, string stderr)> DemultiplexStreamAsync(MultiplexedStream stream, CancellationToken token)
    {
        var stdoutBuffer = new MemoryStream();
        var stderrBuffer = new MemoryStream();
        byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(81920); 
        try
        {
            while (true)
            {
                var result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, token);
                if (result.EOF) break;
                if (result.Target == MultiplexedStream.TargetStream.StandardOut) await stdoutBuffer.WriteAsync(buffer, 0, result.Count, token);
                else if (result.Target == MultiplexedStream.TargetStream.StandardError) await stderrBuffer.WriteAsync(buffer, 0, result.Count, token);
            }
        }
        finally { System.Buffers.ArrayPool<byte>.Shared.Return(buffer); }
        return (Encoding.UTF8.GetString(stdoutBuffer.ToArray()), Encoding.UTF8.GetString(stderrBuffer.ToArray()));
    }
    
    public async Task PauseContainerAsync(string containerId)
    {
        await dockerClient.Containers.PauseContainerAsync(containerId, CancellationToken.None);
    }

    public async Task UnpauseContainerAsync(string containerId)
    {
        await dockerClient.Containers.UnpauseContainerAsync(containerId, CancellationToken.None);
    }
}