using Domain.Model;
using Domain.Models.Provision;

namespace Application.Services.Interfaces;

public interface INodeService
{
    /// <summary>
    /// Orchestrates the process of provisioning a new Xray container instance on a physical node server.
    /// </summary>
    Task<ProvisionResponseDto> ProvisionContainerAsync(ProvisionRequestDto request);

    /// <summary>
    /// Requests to deprovision (stop and delete) an existing X-ray container instance.
    /// </summary>
    Task DeprovisionContainerAsync(long instanceId);

    /// <summary>
    /// Retrieves the current status of a provisioned container.
    /// </summary>
    Task<string> GetContainerStatusAsync(string containerId);

    /// <summary>
    /// Retrieves logs from a provisioned container.
    /// </summary>
    Task<string> GetContainerLogsAsync(string containerId);
    
    /// <summary>
    /// Pauses a provisioned container.
    /// </summary>
    Task<string> PauseContainerAsync(string containerId);

    /// <summary>
    /// Resumes (unpauses) a provisioned container.
    /// </summary>
    Task<string> ResumeContainerAsync(string containerId);
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    Task<IEnumerable<InstanceInfo>> GetAllLocalInstancesAsync();
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="instanceId"></param>
    /// <returns></returns>
    Task<string> GetInstanceTrafficAsync(long instanceId);
}