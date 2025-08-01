﻿using Domain.Model;
using Domain.Models.Provision;

namespace Application.Services.Interfaces;

public interface INodeService
{
    /// <summary>
    /// Orchestrates the process of provisioning a new Xray container instance on a physical node server.
    /// </summary>
    /// <param name="request">Details required for provisioning, including credentials and container image.</param>
    /// <returns>Details of the provisioned container, including allocated ports and Docker ID.</returns>
    Task<ProvisionResponseDto> ProvisionContainerAsync(ProvisionRequestDto request);

    /// <summary>
    /// Requests to deprovision (stop and delete) an existing Xray container instance.
    /// </summary>
    /// <param name="containerId">The Docker container ID or name of the instance to deprovision.</param>
    /// <returns>A success message indicating the deprovisioning status.</returns>
    Task<string> DeprovisionContainerAsync(string containerId);

    /// <summary>
    /// Retrieves the current status of a provisioned container.
    /// </summary>
    /// <param name="containerId">The Docker container ID or name.</param>
    /// <returns>The status of the container (e.g., "running", "exited").</returns>
    Task<string> GetContainerStatusAsync(string containerId);

    /// <summary>
    /// Retrieves logs from a provisioned container.
    /// </summary>
    /// <param name="containerId">The Docker container ID or name.</param>
    /// <returns>The container logs as a string.</returns>
    Task<string> GetContainerLogsAsync(string containerId);
    
       
    /// <summary>
    /// Pauses a provisioned container.
    /// </summary>
    /// <param name="containerId">The Docker container ID or name.</param>
    Task<string> PauseContainerAsync(string containerId);

    /// <summary>
    /// Resumes (unpauses) a provisioned container.
    /// </summary>
    /// <param name="containerId">The Docker container ID or name.</param>
    Task<string> ResumeContainerAsync(string containerId);
    /// <summary>
    /// this for get instance traffic
    /// </summary>
    /// <param name="instanceId"></param>
    /// <returns></returns>
    Task<TrafficUsageDto> GetInstanceTrafficAsync(long instanceId);
}