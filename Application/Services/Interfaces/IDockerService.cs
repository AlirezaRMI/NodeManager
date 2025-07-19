namespace Application.Services.Interfaces;

public interface IDockerService
{
    /// <summary>
    /// Executes a shell command and captures its output.
    /// </summary>
    /// <param name="command">The command to execute (e.g., "docker", "ufw", "ss").</param>
    /// <param name="arguments">Arguments for the command.</param>
    /// <param name="workingDirectory">Optional: The working directory for the command.</param>
    /// <param name="envVars">Optional: Environment variables for the command.</param>
    /// <returns>A tuple containing the exit code, standard output, and standard error.</returns>
    Task<(int ExitCode, string StdOut, string StdErr)> ExecuteShellCommandAsync(
        string command, string arguments, string? workingDirectory = null, Dictionary<string, string>? envVars = null);

    /// <summary>
    /// Creates and starts a new Docker container.
    /// </summary>
    /// <param name="imageName">The Docker image name (e.g., "xray/xray:latest").</param>
    /// <param name="containerName">The unique name for the new container.</param>
    /// <param name="portMappings">List of port mappings (e.g., "8080:80").</param>
    /// <param name="environmentVariables">Environment variables to pass to the container.</param>
    /// <param name="command">Optional: Command to run inside the container, overriding the image's default.</param>
    /// <param name="configMountPath">Optional: Path inside container to mount config (if configContent is provided).</param>
    /// <param name="configContent">Optional: Content of config file to write/mount (needs specific implementation).</param>
    /// <returns>The Docker container ID if successful.</returns>
    Task<string> CreateContainerAsync(string imageName, string containerName, List<string> portMappings,
        Dictionary<string, string> environmentVariables, string? command = null, string? configMountPath = null,
        string? configContent = null);

    /// <summary>
    /// Stops a running Docker container.
    /// </summary>
    /// <param name="id">ID or name of the container to stop.</param>
    /// <returns>A message indicating success.</returns>
    Task<string> StopContainerAsync(string id);

    /// <summary>
    /// Starts a stopped Docker container.
    /// </summary>
    /// <param name="id">ID or name of the container to start.</param>
    /// <returns>A message indicating success.</returns>
    Task<string> StartContainerAsync(string id);

    /// <summary>
    /// Deletes a Docker container.
    /// </summary>
    /// <param name="id">ID or name of the container to delete.</param>
    /// <returns>A message indicating success.</returns>
    Task<string> DeleteContainerAsync(string id);

    /// <summary>
    /// Restarts a Docker container.
    /// </summary>
    /// <param name="id">ID or name of the container to restart.</param>
    /// <returns>A message indicating success.</returns>
    Task<string> RestartContainerAsync(string id);

    /// <summary>
    /// Gets the current status of a Docker container.
    /// </summary>
    /// <param name="id">ID or name of the container.</param>
    /// <returns>The container's status (e.g., "running", "exited").</returns>
    Task<string> GetContainerStatusAsync(string id);

    /// <summary>
    /// Retrieves logs from a Docker container.
    /// </summary>
    /// <param name="id">ID or name of the container.</param>
    /// <returns>The container's logs as a string.</returns>
    Task<string> GetContainerLogsAsync(string id);

    /// <summary>
    /// Checks if a specific port is available (not in use) on the host system.
    /// </summary>
    /// <param name="port">The port number to check.</param>
    /// <returns>True if the port is available; otherwise, false.</returns>
    Task<bool> IsPortAvailableAsync(int port);

    /// <summary>
    /// Opens a specific port in the host's firewall (e.g., using ufw).
    /// </summary>
    /// <param name="port">The port number to open.</param>
    /// <param name="protocol">The protocol (e.g., "tcp", "udp"). Defaults to "tcp".</param>
    /// <returns>A message indicating success.</returns>
    Task<string> OpenFirewallPortAsync(int port, string protocol = "tcp");

    /// <summary>
    /// Closes a specific port in the host's firewall (e.g., using ufw).
    /// </summary>
    /// <param name="port">The port number to close.</param>
    /// <param name="protocol">The protocol (e.g., "tcp", "udp"). Defaults to "tcp".</param>
    /// <returns>A message indicating success.</returns>
    Task<string> CloseFirewallPortAsync(int port, string protocol = "tcp");

    /// <summary>
    /// this for pause specific container
    /// </summary>
    /// <param name="idOrName"></param>
    /// <returns></returns>
    Task<string> PauseContainerAsync(string idOrName);
    
    /// <summary>
    /// this for unpause a specific container 
    /// </summary>
    /// <param name="idOrName"></param>
    /// <returns></returns>
    Task<string> UnpauseContainerAsync(string idOrName);
}