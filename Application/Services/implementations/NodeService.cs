using Application.Services.Interfaces;
using Domain.Model;
using Domain.Models.Provision;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Application.Services.implementations;


public class NodeService(IDockerService docker, ILogger<INodeService> logger) : INodeService
{
    public async Task<ProvisionResponseDto> ProvisionContainerAsync(ProvisionRequestDto r)
    {
        logger.LogInformation("Provision request for Instance {Id}", r.InstanceId);

        if (r.InboundPort <= 0 || r.XrayPort <= 0 || r.ApiPort <= 0)
            throw new ArgumentException("All requested ports must be > 0");

        var sslDir = $"/var/lib/easyhub-instance-data/{r.InstanceId}/ssl";
        var pemPath = Path.Combine(sslDir, "node.pem");

        await docker.CreateDirectoryOnHostAsync(sslDir);
        await docker.WriteFileOnHostAsync(pemPath, r.CertificateKey!);
        ;

        await docker.OpenFirewallPortAsync(r.InboundPort);
        await docker.OpenFirewallPortAsync(r.XrayPort);
        await docker.OpenFirewallPortAsync(r.ApiPort);

        var envVars = new Dictionary<string, string>
        {
            ["SERVICE_PROTOCOL"] = "rest",
            ["SSL_CLIENT_CERT_FILE"] = "/var/lib/marzban-node/ssl/node.pem",
        };

        var volumes = new List<string>
        {
            $"{sslDir}:/var/lib/marzban-node/ssl:ro"
        };

        var ports = new List<string>
        {
            $"{r.InboundPort}:{r.InboundPort}",
            $"{r.XrayPort}:62051",
            $"{r.ApiPort}:62050"
        };

        var mainContainerName = $"easyhub-xray-{r.InstanceId}";
        var sidecarContainerName = $"easyhub-sniffer-{r.InstanceId}";
        var sidecarImageName = "ghcr.io/alirezarmi/sniffer:latest";

        var containerId = await docker.CreateContainerAsync(
            imageName: r.XrayContainerImage,
            containerName: mainContainerName,
            portMappings: ports,
            environmentVariables: envVars,
            volumeMappings: volumes);

        await docker.StartContainerAsync(containerId);
        logger.LogInformation("Container started ({Id})", containerId);

       
        var sidecarContainerId = await docker.CreateContainerAsync(
            imageName: sidecarImageName,
            containerName: sidecarContainerName,
            portMappings: new List<string>(),
            environmentVariables: new Dictionary<string, string>(),
            volumeMappings: new List<string>(),
            networkMode: $"container:{mainContainerName}" 
        );

        await docker.StartContainerAsync(sidecarContainerId);
        logger.LogInformation("Sidecar container started ({Id})", sidecarContainerId);

        return new ProvisionResponseDto
        {
            ProvisionedInstanceId = r.InstanceId,
            IsSuccess = true,
            ContainerDockerId = containerId,
            XrayUserUuid = "UUID_NOT_EXTRACTED",
        };
    }

    public async Task<string> DeprovisionContainerAsync(string id)
    {
        await docker.StopContainerAsync(id);
        await docker.DeleteContainerAsync(id);
        return $"Container {id} removed";
    }

    public Task<string> GetContainerStatusAsync(string id) => docker.GetContainerStatusAsync(id);
    public Task<string> GetContainerLogsAsync(string id)   => docker.GetContainerLogsAsync(id);
    public Task<string> PauseContainerAsync(string id) {  docker.PauseContainerAsync(id);   return Task.FromResult($"{id} paused"); }
    public Task<string> ResumeContainerAsync(string id) { docker.UnpauseContainerAsync(id); return Task.FromResult($"{id} resumed"); }

    public async Task<TrafficUsageDto> GetInstanceTrafficAsync(long instanceId)
    {
        var mainContainerName = $"easyhub-xray-{instanceId}";
        logger.LogInformation("Fetching traffic for container: {ContainerName}", mainContainerName);
        try
        {
            var sidecarContainerName = $"easyhub-sniffer-{instanceId}";
            var command = new[] { "curl", "-s", "http://127.0.0.1:9191/metrics" };
            string trafficJson = await docker.ExecuteCommandInContainerAsync(sidecarContainerName, command);

            if (string.IsNullOrWhiteSpace(trafficJson))
            {
                logger.LogWarning("Received empty response from sniffer for container {ContainerName}", mainContainerName);
                throw new InvalidOperationException("Received empty response from sniffer sidecar.");
            }
            var dto = JsonConvert.DeserializeObject<TrafficUsageDto>(trafficJson); 
            return dto!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute traffic command in container {ContainerName}", mainContainerName);
            throw;
        }
    }
}
