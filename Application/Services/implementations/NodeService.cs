using Application.Services.Interfaces;
using Domain.Models.Provision;
using Microsoft.Extensions.Logging;

namespace Application.Services.implementations;

public class NodeService(IDockerService dockerManager, ILogger<INodeService> logger) : INodeService
{
    private readonly Random _random = new Random();

    private const int MinDynamicPort = 20000;
    private const int MaxDynamicPort = 60000;


    /// <summary>
    /// Orchestrates the process of provisioning a new Xray container instance.
    /// </summary>
    public async Task<ProvisionResponseDto> ProvisionContainerAsync(ProvisionRequestDto request)
    {
        logger.LogInformation("Starting provisioning process for instance ID: {InstanceId} on host {Host}",
            request.InstanceId, request.SshHost);

        string containerName = $"easyhub-xray-{request.InstanceId}";

        int assignedInboundPort;
        int assignedXrayPort;
        int assignedServerPort;

        try
        {
            assignedInboundPort = await GetUniquePortAndOpenFirewallAsync(MinDynamicPort, MaxDynamicPort,
                request.SshHost, request.SshPort, request.SshUsername, request.SshPrivateKey, request.SshPassword,
                62050, 62051);
            assignedXrayPort = await GetUniquePortAndOpenFirewallAsync(MinDynamicPort, MaxDynamicPort, request.SshHost,
                request.SshPort, request.SshUsername, request.SshPrivateKey, request.SshPassword, assignedInboundPort,
                62050, 62051);
            assignedServerPort = await GetUniquePortAndOpenFirewallAsync(MinDynamicPort, MaxDynamicPort,
                request.SshHost, request.SshPort, request.SshUsername, request.SshPrivateKey, request.SshPassword,
                assignedInboundPort, assignedXrayPort, 62050, 62051);

            logger.LogInformation("Allocated ports: Inbound={Inbound}, Xray={Xray}, Server={Server}",
                assignedInboundPort, assignedXrayPort, assignedServerPort);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to allocate unique ports for instance {InstanceId}.", request.InstanceId);
            return new ProvisionResponseDto
            {
                ProvisionedInstanceId = request.InstanceId, IsSuccess = false,
                ErrorMessage = $"Failed to allocate ports: {ex.Message}"
            };
        }

        string xrayUserUuid = Guid.NewGuid().ToString();

        var containerEnvVars = new Dictionary<string, string>
        {
            { "XRAY_UUID", xrayUserUuid },
            { "XRAY_INBOUND_PORT", assignedInboundPort.ToString() },
            { "XRAY_XRAY_PORT", assignedXrayPort.ToString() },
            { "XRAY_SERVER_PORT", assignedServerPort.ToString() },
            { "XRAY_LOG_LEVEL", "warning" },
        };

        string containerDockerId;
        try
        {
            containerDockerId = await dockerManager.CreateContainerAsync(
                request.XrayContainerImage,
                containerName,
                [
                    $"{assignedInboundPort}:{assignedInboundPort}", $"{assignedXrayPort}:{assignedXrayPort}",
                    $"{assignedServerPort}:{assignedServerPort}"
                ],
                containerEnvVars,
                command: null
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create Docker container for instance {InstanceId}.", request.InstanceId);
            await CloseAllocatedPortsAndFirewallAsync(request.SshHost, request.SshPort, request.SshUsername,
                request.SshPrivateKey, request.SshPassword, assignedInboundPort, assignedXrayPort, assignedServerPort);
            return new ProvisionResponseDto
            {
                ProvisionedInstanceId = request.InstanceId, IsSuccess = false,
                ErrorMessage = $"Failed to create container: {ex.Message}"
            };
        }

        logger.LogInformation(
            "Successfully provisioned container {ContainerId} for instance {InstanceId} with ports Inbound={Inbound}, Xray={Xray}, Server={Server}",
            containerDockerId, request.InstanceId, assignedInboundPort, assignedXrayPort, assignedServerPort);

        return new ProvisionResponseDto
        {
            ProvisionedInstanceId = request.InstanceId,
            IsSuccess = true,
            ContainerDockerId = containerDockerId,
            AssignedInboundPort = assignedInboundPort,
            AssignedXrayPort = assignedXrayPort,
            AssignedServerPort = assignedServerPort,
            XrayUserUuid = xrayUserUuid,
        };
    }

    public async Task<string> DeprovisionContainerAsync(long containerId)
    {
        logger.LogInformation("Deprovisioning container {ContainerId}...", containerId);

        await dockerManager.StopContainerAsync(containerId);
        await dockerManager.DeleteContainerAsync(containerId);

        logger.LogInformation("Container {ContainerId} deprovisioned.", containerId);
        return $"Container {containerId} deprovisioned.";
    }

    public async Task<string> GetContainerStatusAsync(long containerId)
    {
        return await dockerManager.GetContainerStatusAsync(containerId);
    }

    public async Task<string> GetContainerLogsAsync(long containerId)
    {
        return await dockerManager.GetContainerLogsAsync(containerId);
    }

    private async Task<int> GetUniquePortAndOpenFirewallAsync(int minPort, int maxPort, string host, int sshPort,
        string sshUsername, string privateKey, string? passphrase, params int[] excludedPorts)
    {
        var maxAttempts = 100;
        for (var i = 0; i < maxAttempts; i++)
        {
            var candidatePort = _random.Next(minPort, maxPort + 1);

            if (excludedPorts.Contains(candidatePort)) continue;

            if (!await dockerManager
                    .IsPortAvailableAsync(candidatePort)) continue;
            await dockerManager.OpenFirewallPortAsync(candidatePort);
            return candidatePort;
        }

        throw new InvalidOperationException("Failed to find a unique available port after multiple attempts.");
    }

    private async Task CloseAllocatedPortsAndFirewallAsync(string host, int sshPort, string sshUsername,
        string privateKey, string? passphrase, params int[] portsToClose)
    {
        foreach (var port in portsToClose)
        {
            try
            {
                await dockerManager.CloseFirewallPortAsync(port);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to close port {Port} in firewall during error handling.", port);
            }
        }
    }
}