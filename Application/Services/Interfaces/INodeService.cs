using Domain.Models.Provision;

namespace Application.Services.Interfaces;

public interface INodeService
{
    /// <summary>
    /// Orchestrates the process of provisioning a new Xray container instance on a physical node server.
    /// </summary>
    /// <param name="request">Details required for provisioning, including SSH credentials and container image.</param>
    /// <returns>Details of the provisioned container, including allocated ports and Docker ID.</returns>
    Task<ProvisionResponseDto> ProvisionContainerAsync(ProvisionRequestDto request);

    /// <summary>
    /// Requests to deprovision (stop and delete) an existing Xray container instance.
    /// </summary>
    /// <param name="id">The Docker container ID or name of the instance to depression.</param>
    /// <returns>A success message indicating the deprovisioning status.</returns>
    Task<string> DeprovisionContainerAsync(string id);

    /// <summary>
    /// Retrieves the current status of a provisioned container.
    /// </summary>
 
    /// <param name="id"></param>
    /// <returns>The status of the container (e.g., "running", "exited").</returns>
    Task<string> GetContainerStatusAsync(string id);

    /// <summary>
    /// Retrieves logs from a provisioned container.
    /// </summary>
    /// <param name="id">The Docker container ID or name of the instance.</param>
    /// <returns>The container logs as a string.</returns>
    Task<string> GetContainerLogsAsync(string id);
    /// <summary>
    /// this for pause specific container
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    Task<string> PauseContainerAsync(string id);
    
    /// <summary>
    /// this for unpause a specific container
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    Task<string> ResumeContainerAsync(string id);
}