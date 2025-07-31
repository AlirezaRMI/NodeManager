namespace Application.Services.Interfaces;

/// <summary>
/// Defines a service for managing the complete lifecycle of Docker containers 
/// and handling necessary interactions with the host operating system.
/// </summary>
public interface IDockerService
{
    /// <summary>
    /// Creates a new Docker container using the Docker SDK. It does not start the container.
    /// </summary>
    /// <param name="imageName">The Docker image to use.</param>
    /// <param name="containerName">A unique name for the container.</param>
    /// <param name="portMappings">A list of port mappings (e.g., "8080:80"). Can be empty for host network mode.</param>
    /// <param name="environmentVariables">Environment variables for the container.</param>
    /// <param name="volumeMappings">A list of volume/bind mounts (e.g., "/host/path:/container/path").</param>
    /// <param name="command">Optional command to run in the container.</param>
    /// <param name="networkMode">The network mode for the container (e.g., "bridge", "host").</param>
    /// <returns>The ID of the newly created container.</returns>
    Task<string> CreateContainerAsync(
        string imageName,
        string containerName,
        List<string> portMappings,
        Dictionary<string, string> environmentVariables,
        List<string> volumeMappings,
        string? command = null,
        string? networkMode = null);

    /// <summary>
    /// Starts a previously created container.
    /// </summary>
    /// <param name="containerId">The ID or name of the container to start.</param>
    Task StartContainerAsync(string containerId);

    /// <summary>
    /// Stops a running container.
    /// </summary>
    /// <param name="containerId">The ID or name of the container to stop.</param>
    Task StopContainerAsync(string containerId);

    /// <summary>
    /// Deletes a container. It should be stopped first.
    /// </summary>
    /// <param name="containerId">The ID or name of the container to delete.</param>
    Task DeleteContainerAsync(string containerId);

    /// <summary>
    /// Gets the current status of a container (e.g., "running", "exited").
    /// </summary>
    /// <param name="containerId">The ID or name of the container.</param>
    Task<string> GetContainerStatusAsync(string containerId);

    /// <summary>
    /// Retrieves logs from a container.
    /// </summary>
    /// <param name="containerId">The ID or name of the container.</param>
    Task<string> GetContainerLogsAsync(string containerId);

    /// <summary>
    /// Executes a command inside a running container.
    /// </summary>
    /// <param name="containerId">The ID of the container.</param>
    /// <param name="command">The command to execute as an array of strings (e.g., new[] { "ls", "-l" }).</param>
    /// <returns>The standard output of the command.</returns>
    Task<string> ExecuteCommandInContainerAsync(string containerId, string[] command);

    /// <summary>
    /// Opens a specific port in the host's firewall (ufw).
    /// </summary>
    Task OpenFirewallPortAsync(int port, string protocol = "tcp");

    /// <summary>
    /// Closes a specific port in the host's firewall (ufw).
    /// </summary>
    Task CloseFirewallPortAsync(int port, string protocol = "tcp");

    /// <summary>
    /// Writes content to a file on the host OS, typically for certificates.
    /// </summary>
    Task WriteFileOnHostAsync(string filePath, string content);

    /// <summary>
    /// Creates a directory on the host OS.
    /// </summary>
    Task CreateDirectoryOnHostAsync(string path);

    /// <summary>
    /// Pauses all processes within a running container.
    /// </summary>
    /// <param name="containerId">The ID or name of the container to pause.</param>
    Task PauseContainerAsync(string containerId);

    /// <summary>
    /// Unpauses all processes within a paused container.
    /// </summary>
    /// <param name="containerId">The ID or name of the container to unpause.</param>
    Task UnpauseContainerAsync(string containerId);
}