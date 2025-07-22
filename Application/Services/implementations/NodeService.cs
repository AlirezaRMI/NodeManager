using Application.Services.Interfaces;
using Domain.Models.Provision;
using Microsoft.Extensions.Logging;

namespace Application.Services.implementations;

public class NodeService(IDockerService dockerManager, ILogger<INodeService> logger) : INodeService
{
    public async Task<ProvisionResponseDto> ProvisionContainerAsync(ProvisionRequestDto request)
    {
        logger.LogInformation("Starting provisioning process for instance ID: {InstanceId}", request.InstanceId);

        string containerName = $"easyhub-{request.InstanceId}";
        int assignedInboundPort = request.InboundPort ?? throw new InvalidOperationException("Inbound Port is required.");
        int assignedXrayPort = request.XrayPort ?? throw new InvalidOperationException("Xray Port is required.");
        int assignedServerPort = request.ServerPort ?? throw new InvalidOperationException("Server Port is required.");
        string containerDockerId;
        string xrayUserUuid = "UUID_NOT_EXTRACTED";

        try
        {
            // Step 1: Host setup (directories, files, firewall)
            string instanceDataPath = $"/var/lib/marzban-node-{request.InstanceId}";
            await dockerManager.CreateDirectoryOnHostAsync(instanceDataPath);
            await dockerManager.WriteFileOnHostAsync(Path.Combine(instanceDataPath, "ssl_client_cert.pem"), request.SshPrivateKey);
            await dockerManager.OpenFirewallPortAsync(assignedInboundPort);
            await dockerManager.OpenFirewallPortAsync(assignedXrayPort);
            await dockerManager.OpenFirewallPortAsync(assignedServerPort);

            // Step 2: Prepare container parameters
            var envVars = new Dictionary<string, string>
            {
                { "SERVICE_PORT", assignedInboundPort.ToString() },
                { "XRAY_API_PORT", assignedXrayPort.ToString() },
                { "SERVICE_PROTOCOL", "rest" },
                { "SSL_CLIENT_CERT_FILE", "/var/lib/marzban-node/ssl_client_cert.pem" }
            };
            var volumes = new List<string>
            {
                $"{instanceDataPath}:/var/lib/marzban-node",
                $"{instanceDataPath}:/var/lib/marzban"
            };

            // Step 3: Create and Start the container
            containerDockerId = await dockerManager.CreateContainerAsync(
                imageName: request.XrayContainerImage,
                containerName: containerName,
                portMappings: new List<string>(), // Not needed for host network mode
                environmentVariables: envVars,
                volumeMappings: volumes,
                networkMode: "host"
            );
            await dockerManager.StartContainerAsync(containerDockerId);
            logger.LogInformation("Container '{Name}' created and started with ID: {Id}", containerName, containerDockerId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create Docker container for instance {InstanceId}.", request.InstanceId);
            await CloseAllocatedPortsAndFirewallAsync(assignedInboundPort, assignedXrayPort, assignedServerPort);
            return new ProvisionResponseDto { IsSuccess = false, ErrorMessage = $"Failed to create container: {ex.Message}" };
        }

        // Step 4: Extract UUID from the running container
        try
        {
            const int maxAttempts = 10;
            var delayBetweenAttempts = TimeSpan.FromSeconds(2);
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var lsOutput = await dockerManager.ExecuteCommandInContainerAsync(containerDockerId, new[] { "ls", "/var/lib/marzban/users" });
                if (!string.IsNullOrEmpty(lsOutput))
                {
                    var userJsonFile = lsOutput.Trim().Split('\n').FirstOrDefault(f => f.EndsWith(".json"));
                    if (userJsonFile != null)
                    {
                        var uuidOutput = await dockerManager.ExecuteCommandInContainerAsync(containerDockerId, new[] { "jq", "-r", ".id", $"/var/lib/marzban/users/{userJsonFile}" });
                        if (!string.IsNullOrEmpty(uuidOutput))
                        {
                            xrayUserUuid = uuidOutput.Trim();
                            logger.LogInformation("Successfully extracted UUID on attempt {Attempt}: {UUID}", attempt, xrayUserUuid);
                            break;
                        }
                    }
                }
                if (attempt < maxAttempts) await Task.Delay(delayBetweenAttempts);
                else logger.LogWarning("Failed to extract UUID after {MaxAttempts} attempts.", maxAttempts);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "An error occurred while extracting UUID from container {ContainerId}.", containerDockerId);
        }

        // Step 5: Return the final response
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

    public async Task<string> DeprovisionContainerAsync(string containerId)
    {
        logger.LogInformation("Deprovisioning container {ContainerId}...", containerId);
        await dockerManager.StopContainerAsync(containerId);
        await dockerManager.DeleteContainerAsync(containerId);
        logger.LogInformation("Container {ContainerId} deprovisioned.", containerId);
        return $"Container {containerId} deprovisioned.";
    }

    public Task<string> GetContainerStatusAsync(string containerId) => dockerManager.GetContainerStatusAsync(containerId);
    public Task<string> GetContainerLogsAsync(string containerId) => dockerManager.GetContainerLogsAsync(containerId);

    private async Task CloseAllocatedPortsAndFirewallAsync(params int[] portsToClose)
    {
        foreach (var port in portsToClose)
        {
            try { await dockerManager.CloseFirewallPortAsync(port); }
            catch (Exception ex) { logger.LogError(ex, "Failed to close port {Port} in firewall.", port); }
        }
    }
    
    public async Task<string> PauseContainerAsync(string containerId)
    {
        await dockerManager.PauseContainerAsync(containerId);
        return $"Container {containerId} paused.";
    }

    public async Task<string> ResumeContainerAsync(string containerId)
    {
        // Docker's term is "Unpause", so we call that method.
        await dockerManager.UnpauseContainerAsync(containerId);
        return $"Container {containerId} resumed.";
    }
}