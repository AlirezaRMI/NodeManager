using System.Diagnostics;
using System.Runtime.InteropServices;
using Application.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services.implementations;

public class DockerService(ILogger<IDockerService> logger) : IDockerService
{
    public async Task<(int ExitCode, string StdOut, string StdErr)> ExecuteShellCommandAsync(
        string command, string arguments, string? workingDirectory = null, Dictionary<string, string>? envVars = null)
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var shell = isWindows ? "cmd.exe" : "/bin/bash";
        var shellArgs = isWindows ? $"/c {command} {arguments}" : $"-c \"{command} {arguments}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = shell,
            Arguments = shellArgs,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (envVars != null)
        {
            foreach (var kvp in envVars)
            {
                startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
            }
        }

        using var process = new Process();
        process.StartInfo = startInfo;
        logger.LogDebug("Executing shell command: '{Command} {Arguments}' in '{WorkingDirectory}'", command, arguments,
            startInfo.WorkingDirectory);
        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            logger.LogError("Command failed (ExitCode: {ExitCode}). Stderr: {StdErr}. Stdout: {StdOut}",
                process.ExitCode, stderr, stdout);
        }
        else
        {
            logger.LogDebug("Command successful. Stdout: {StdOut}", stdout);
        }

        return (process.ExitCode, stdout, stderr);
    }


    public async Task<string> CreateContainerAsync(string imageName, string containerName, List<string> portMappings,
        Dictionary<string, string> environmentVariables, string? command = null, string? configMountPath = null,
        string? configContent = null)
    {
        logger.LogInformation("Creating Xray container '{ContainerName}' from image '{ImageName}'...", containerName,
            imageName);

        var pArgs = string.Join(" ", portMappings.Select(p => $"-p {p}"));
        var eArgs = string.Join(" ", environmentVariables.Select(kv => $"-e {kv.Key}={kv.Value}"));

        if (configMountPath != null && configContent != null)
        {
            logger.LogWarning(
                "Direct config mount via string content not fully supported by simple docker CLI run. Requires advanced setup or a custom Xray image.");
        }

        var dockerCommandArgs = $"run -d --name {containerName} {pArgs} {eArgs} {imageName} {command}";

        var result = await ExecuteShellCommandAsync("docker", dockerCommandArgs);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to create container: {result.StdErr}");
        }

        var id = result.StdOut.Trim();
        logger.LogInformation("Container '{ContainerName}' created with ID: {ContainerId}", containerName, id);
        return id;
    }

    public async Task<string> StopContainerAsync(long idOrName)
    {
        logger.LogInformation("Stopping container '{ContainerIdOrName}'...", idOrName);
        await ExecuteShellCommandAsync("docker", $"stop {idOrName.ToString()}");
        return $"Container {idOrName} stopped.";
    }

    public async Task<string> StartContainerAsync(long id)
    {
        logger.LogInformation("Starting container with ID {ContainerId}...", id);
        await ExecuteShellCommandAsync("docker", $"start {id.ToString()}");
        return $"Container {id} started.";
    }

    public async Task<string> DeleteContainerAsync(long id)
    {
        logger.LogInformation("Deleting container with ID {ContainerId}...", id);
        await ExecuteShellCommandAsync("docker", $"rm -f {id.ToString()}");
        return $"Container {id} deleted.";
    }

    public async Task<string> RestartContainerAsync(long id)
    {
        logger.LogInformation("Restarting container with ID {ContainerId}...", id);
        await ExecuteShellCommandAsync("docker", $"restart {id.ToString()}");
        return $"Container {id} restarted.";
    }

    public async Task<string> GetContainerStatusAsync(long id)
    {
        logger.LogInformation("Getting status for container with ID {ContainerId}...", id);
        var result =
            await ExecuteShellCommandAsync("docker", $"ps -a --filter id={id.ToString()} --format '{{.Status}}'");
        if (string.IsNullOrEmpty(result.StdOut.Trim()))
        {
            throw new InvalidOperationException($"Container '{id}' not found.");
        }

        return result.StdOut.Trim();
    }

    public async Task<string> GetContainerLogsAsync(long id)
    {
        logger.LogInformation("Getting logs for container with ID {ContainerId}...", id);
        var result = await ExecuteShellCommandAsync("docker", $"logs {id.ToString()}");
        return result.StdOut;
    }

    public async Task<string> ExecuteDockerCliCommandAsync(string arguments)
    {
        logger.LogInformation("Executing custom Docker CLI command: '{Arguments}'", arguments);
        var result = await ExecuteShellCommandAsync("docker", arguments);
        return result.StdOut;
    }

    public async Task<bool> IsPortAvailableAsync(int port)
    {
        logger.LogInformation("Checking if port {Port} is available on host...", port);
        var result = await ExecuteShellCommandAsync("ss", $"-tuln | grep -w \":{port}\"");

        bool isTaken = result.ExitCode == 0 && !string.IsNullOrEmpty(result.StdOut);
        logger.LogInformation("Port {Port} is available: {IsAvailable}", port, !isTaken);
        return !isTaken;
    }

    public async Task<string> OpenFirewallPortAsync(int port, string protocol = "tcp")
    {
        logger.LogInformation("Attempting to open port {Port}/{Protocol} in firewall (ufw)...", port, protocol);

        var result = await ExecuteShellCommandAsync("sudo", $"ufw allow {port}/{protocol}");
        if (result.ExitCode != 0 && !result.StdErr.Contains("Rule already exists"))
        {
            logger.LogError("Failed to open port {Port}/{Protocol} in firewall. Stderr: {StdErr}", port, protocol,
                result.StdErr);
            throw new InvalidOperationException($"Failed to open port {port}/{protocol} in firewall: {result.StdErr}");
        }

        logger.LogInformation("Port {Port}/{Protocol} opened/verified in firewall.", port, protocol);
        return $"Port {port}/{protocol} opened/verified.";
    }

    public async Task<string> CloseFirewallPortAsync(int port, string protocol = "tcp")
    {
        logger.LogInformation("Attempting to close port {Port}/{Protocol} in firewall (ufw)...", port, protocol);

        var result = await ExecuteShellCommandAsync("sudo", $"ufw delete allow {port}/{protocol}");
        if (result.ExitCode != 0 && !result.StdErr.Contains("No matching rule"))
        {
            logger.LogError("Failed to close port {Port}/{Protocol} in firewall. Stderr: {StdErr}", port, protocol,
                result.StdErr);
            throw new InvalidOperationException($"Failed to close port {port}/{protocol} in firewall: {result.StdErr}");
        }

        logger.LogInformation("Port {Port}/{Protocol} closed/verified in firewall.", port, protocol);
        return $"Port {port}/{protocol} closed/verified.";
    }
}