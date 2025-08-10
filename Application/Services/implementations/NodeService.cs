using Application.Services.Interfaces;
using Domain.Model;
using Domain.Models.Provision;
using Microsoft.Extensions.Logging;

namespace Application.Services.implementations;

public class NodeService(IDockerService docker, ILogger<INodeService> logger, ILocalInstanceStore localInstanceStore)
    : INodeService
{
    public async Task<ProvisionResponseDto> ProvisionContainerAsync(ProvisionRequestDto r)
    {
        logger.LogInformation("Provision request for Instance {Id}", r.InstanceId);
        if (r.InboundPort <= 0 || r.XrayPort <= 0 || r.ApiPort <= 0)
            throw new ArgumentException("All requested ports must be > 0");

        await docker.OpenFirewallPortAsync(r.InboundPort);
        await docker.OpenFirewallPortAsync(r.XrayPort);
        await docker.OpenFirewallPortAsync(r.ApiPort);

        await docker.AddTrafficCountingRuleAsync(r.InboundPort);
        
        var sslDir = $"/var/lib/easyhub-instance-data/{r.InstanceId}/ssl";
        var pemPath = Path.Combine(sslDir, "node.pem");

        await docker.CreateDirectoryOnHostAsync(sslDir);
        await docker.WriteFileOnHostAsync(pemPath, r.CertificateKey!);

        var envVars = new Dictionary<string, string>
        {
            ["SERVICE_PROTOCOL"] = "rest",
            ["SSL_CLIENT_CERT_FILE"] = "/var/lib/marzban-node/ssl/node.pem",
        };

        var volumes = new List<string> { $"{sslDir}:/var/lib/marzban-node/ssl:ro" };
        var ports = new List<string> { $"{r.InboundPort}:{r.InboundPort}", $"{r.XrayPort}:62051", $"{r.ApiPort}:62050" };
        var mainContainerName = $"easyhub-xray-{r.InstanceId}";

        var containerId = await docker.CreateContainerAsync(
            imageName: r.XrayContainerImage,
            containerName: mainContainerName,
            portMappings: ports,
            environmentVariables: envVars,
            volumeMappings: volumes,
            networkMode: "easynet");

        await docker.StartContainerAsync(containerId);

        await localInstanceStore.AddAsync(new InstanceInfo { Id = r.InstanceId, InboundPort = r.InboundPort });

        logger.LogInformation("Container started ({Id})", containerId);
        return new ProvisionResponseDto
        {
            ProvisionedInstanceId = r.InstanceId, IsSuccess = true, ContainerDockerId = containerId,
            XrayUserUuid = "UUID_NOT_EXTRACTED",
        };
    }

    public async Task<string> DeprovisionContainerAsync(string id, long instanceId)
    {
        var instanceInfo = (await localInstanceStore.GetAllAsync()).FirstOrDefault(i => i.Id == instanceId);
        if (instanceInfo != null)
        {
            await docker.RemoveTrafficCountingRuleAsync(instanceInfo.InboundPort);
        }

        await docker.StopContainerAsync(id);
        await docker.DeleteContainerAsync(id);
        await localInstanceStore.RemoveAsync(instanceId);
        return $"Container {id} removed";
    }

    public Task<string> GetContainerStatusAsync(string id) => docker.GetContainerStatusAsync(id);
    public Task<string> GetContainerLogsAsync(string id) => docker.GetContainerLogsAsync(id);

    public async Task<string> PauseContainerAsync(string id)
    {
        await docker.PauseContainerAsync(id);
        return $"{id} paused";
    }

    public async Task<string> ResumeContainerAsync(string id)
    {
        await docker.UnpauseContainerAsync(id);
        return $"{id} unpaused";
    }
}