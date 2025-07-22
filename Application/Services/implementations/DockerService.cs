using System.Buffers;
using System.Text;
using System.Diagnostics;
using Application.Services.Interfaces;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace Application.Services.implementations;

public class DockerService(IDockerClient dockerClient, ILogger<IDockerService> logger) : IDockerService
{
    public async Task<string> CreateContainerAsync(string imageName, string containerName, List<string> portMappings,
        Dictionary<string, string> environmentVariables, List<string> volumeMappings, string? command = null,
        string networkMode = "bridge")
    {
        var portBindings = portMappings.ToDictionary(
            p => $"{p.Split(':')[1]}/tcp",
            p => (IList<PortBinding>) new List<PortBinding> {new() {HostPort = p.Split(':')[0]}}
        );

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

        var response = await dockerClient.Containers.CreateContainerAsync(createParams);
        return response.ID;
    }

    public async Task StartContainerAsync(string containerId)
    {
        await dockerClient.Containers.StartContainerAsync(containerId, new ContainerStartParameters());
    }

    public async Task StopContainerAsync(string containerId)
    {
        await dockerClient.Containers.StopContainerAsync(containerId, new ContainerStopParameters());
    }

    public async Task DeleteContainerAsync(string containerId)
    {
        await dockerClient.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters {Force = true});
    }

    public async Task<string> GetContainerStatusAsync(string containerId)
    {
        return (await dockerClient.Containers.InspectContainerAsync(containerId)).State.Status;
    }

    [Obsolete("Obsolete")]
    public async Task<string> GetContainerLogsAsync(string containerId)
    {
        var parameters = new ContainerLogsParameters {ShowStdout = true, ShowStderr = true, Timestamps = true};

        await using var stream =
            await dockerClient.Containers.GetContainerLogsAsync(containerId, parameters, CancellationToken.None);
        dynamic dynamicStream = stream;

        if (dynamicStream is MultiplexedStream multiplexedStream)
        {
            logger.LogDebug("Log stream is multiplexed. Demultiplexing...");
            var (stdout, stderr) = await DemultiplexStreamAsync(multiplexedStream, CancellationToken.None);
            return stdout + (string.IsNullOrEmpty(stderr) ? "" : $"\n--- STDERR ---\n{stderr}");
        }
        else
        {
            logger.LogDebug("Log stream is not multiplexed. Reading directly...");
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
    }

    public async Task<string> ExecuteCommandInContainerAsync(string containerId, string[] command)
    {
        var execCreate = await dockerClient.Exec.ExecCreateContainerAsync(containerId,
            new ContainerExecCreateParameters {AttachStdout = true, AttachStderr = true, Cmd = command});
        using var stream = await dockerClient.Exec.StartAndAttachContainerExecAsync(execCreate.ID, false);
        var (stdout, _) = await DemultiplexStreamAsync(stream, CancellationToken.None);
        return stdout.Trim();
    }

    public async Task CreateDirectoryOnHostAsync(string path) => await ExecuteShellCommandAsync("mkdir", $"-p {path}");

    public async Task OpenFirewallPortAsync(int port, string protocol = "tcp") =>
        await ExecuteShellCommandAsync("sudo", $"ufw allow {port}/{protocol}");

    public async Task CloseFirewallPortAsync(int port, string protocol = "tcp") =>
        await ExecuteShellCommandAsync("sudo", $"ufw delete allow {port}/{protocol}");

    public async Task WriteFileOnHostAsync(string filePath, string content)
    {
        var tempFilePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFilePath, content);
        try
        {
            await ExecuteShellCommandAsync("sudo", $"bash -c \"cat {tempFilePath} > {filePath}\"");
        }
        finally
        {
            File.Delete(tempFilePath);
        }
    }

    private async Task<(int ExitCode, string stdout, string stderr)> ExecuteShellCommandAsync(string command,
        string arguments)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash", Arguments = $"-c \"{command} {arguments}\"", RedirectStandardOutput = true,
                RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
            }
        };
        process.Start();
        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            logger.LogWarning("Shell command '{Cmd} {Args}' failed with code {Code}. Stderr: {Err}", command, arguments,
                process.ExitCode, stderr);
        }

        return (process.ExitCode, stdout, stderr);
    }

    private static async Task<(string stdout, string stderr)> DemultiplexStreamAsync(MultiplexedStream stream,
        CancellationToken token)
    {
        var stdoutBuffer = new MemoryStream();
        var stderrBuffer = new MemoryStream();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            while (true)
            {
                var result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, token);
                if (result.EOF) break;
                if (result.Target == MultiplexedStream.TargetStream.StandardOut)
                    await stdoutBuffer.WriteAsync(buffer, 0, result.Count, token);
                else if (result.Target == MultiplexedStream.TargetStream.StandardError)
                    await stderrBuffer.WriteAsync(buffer, 0, result.Count, token);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return (Encoding.UTF8.GetString(stdoutBuffer.ToArray()), Encoding.UTF8.GetString(stderrBuffer.ToArray()));
    }
    
    public async Task<string> PauseContainerAsync(string idOrName)
    {
        logger.LogInformation("Pausing container '{ContainerIdOrName}'...", idOrName);
        var result = await ExecuteShellCommandAsync("docker", $"pause {idOrName}");

        if (result.ExitCode != 0)
        {
            logger.LogError("Failed to pause container '{ContainerIdOrName}'. Stderr: {StdErr}", idOrName, result.StdErr);
            throw new InvalidOperationException($"Failed to pause container {idOrName}: {result.StdErr}");
        }

        logger.LogInformation("Container '{ContainerIdOrName}' paused successfully.", idOrName);
        return $"Container {idOrName} paused.";
    }
    
    public async Task<string> UnpauseContainerAsync(string idOrName)
    {
        logger.LogInformation("Unpausing container '{ContainerIdOrName}'...", idOrName);
        var result = await ExecuteShellCommandAsync("docker", $"unpause {idOrName}");

        if (result.ExitCode != 0)
        {
            logger.LogError("Failed to unpause container '{ContainerIdOrName}'. Stderr: {StdErr}", idOrName, result.StdErr);
            throw new InvalidOperationException($"Failed to unpause container {idOrName}: {result.StdErr}");
        }

        logger.LogInformation("Container '{ContainerIdOrName}' unpaused successfully.", idOrName);
        return $"Container {idOrName} unpaused.";
    }
}