using Docker.DotNet.Models;

namespace Application.Services.Interfaces;

/// <summary>
/// Defines a service for managing the complete lifecycle of Docker containers 
/// and handling necessary interactions with the host operating system.
/// </summary>
public interface IDockerService
{
    Task<string> CreateContainerAsync(
        string imageName, string containerName, List<string> portMappings,
        Dictionary<string, string> environmentVariables, List<string> volumeMappings,
        string? command = null, string? networkMode = null);
    
    Task StartContainerAsync(string id);
    Task StopContainerAsync(string id);
    Task DeleteContainerAsync(string id);
    Task<ContainerStatsResponse> GetContainerStatsAsync(string id);
    Task<string> GetContainerStatusAsync(string id);
    Task PauseContainerAsync(string id);
    Task UnpauseContainerAsync(string id);
    Task CreateDirectoryOnHostAsync(string path);
    Task WriteFileOnHostAsync(string filePath, string content);
    Task OpenFirewallPortAsync(int p, string proto = "tcp");
    Task<string> ExecuteCommandOnHostAsync(string command, string args);
    Task<string> GetContainerLogsAsync(string id);
    Task CloseFirewallPortAsync(int p, string proto = "tcp");
}