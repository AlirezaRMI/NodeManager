using Application.Services.Interfaces;
using Domain.Model;
using Domain.Models.Provision;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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

        var sslDir = $"/var/lib/easyhub-instance-data/{r.InstanceId}/ssl";
        var pemPath = Path.Combine(sslDir, "node.pem");

        await docker.CreateDirectoryOnHostAsync(sslDir);
        await docker.WriteFileOnHostAsync(pemPath, r.CertificateKey!);

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

        var containerId = await docker.CreateContainerAsync(
            imageName: r.XrayContainerImage,
            containerName: mainContainerName,
            portMappings: ports,
            environmentVariables: envVars,
            volumeMappings: volumes,
            networkMode: null);

        await docker.StartContainerAsync(containerId);

        await localInstanceStore.AddAsync(new InstanceInfo { Id = r.InstanceId, InboundPort = r.InboundPort });

        logger.LogInformation("Container started ({Id})", containerId);
        return new ProvisionResponseDto
        {
            ProvisionedInstanceId = r.InstanceId,
            IsSuccess = true,
            ContainerDockerId = containerId,
        };
    }

    public async Task DeprovisionContainerAsync(long instanceId)
    {
        var mainContainerName = $"easyhub-xray-{instanceId}";
        logger.LogInformation("Starting deprovision process for instance {InstanceId} (container: {ContainerName})",
            instanceId, mainContainerName);

        await docker.StopContainerAsync(mainContainerName);
        await docker.DeleteContainerAsync(mainContainerName);
        logger.LogInformation("Container {ContainerName} stopped and removed.", mainContainerName);

        var instanceInfo = (await localInstanceStore.GetAllAsync()).FirstOrDefault(i => i.Id == instanceId);
        if (instanceInfo != null)
        {
            await docker.CloseFirewallPortAsync(instanceInfo.InboundPort);
            logger.LogInformation("Firewall port {Port} closed.", instanceInfo.InboundPort);

            await localInstanceStore.RemoveAsync(instanceId);
            logger.LogInformation("Instance info removed from local store.");
        }

        var instanceDir = $"/var/lib/easyhub-instance-data/{instanceId}";
        await docker.ExecuteCommandOnHostAsync("rm", $"-rf {instanceDir}");
        logger.LogInformation("Instance directory {Directory} removed from host.", instanceDir);

        logger.LogInformation("Successfully deprovisioned all resources for instance {InstanceId}", instanceId);
    }

    public Task<string> GetContainerStatusAsync(string id) => docker.GetContainerStatusAsync(id);

    public async Task<string> GetContainerLogsAsync(string id)
    {
        logger.LogInformation("Fetching logs for container ID: {ContainerId}", id);
        try
        {
            var logs = await docker.GetContainerLogsAsync(id);
            logger.LogInformation("Successfully fetched logs for container ID: {ContainerId}", id);
            return logs;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get logs for container ID: {ContainerId}", id);
            throw;
        }
    }

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

    public async Task<IEnumerable<InstanceInfo>> GetAllLocalInstancesAsync()
    {
        return await localInstanceStore.GetAllAsync();
    }

    public async Task<string> GetInstanceTrafficAsync(long instanceId)
    {
        var mainContainerName = $"easyhub-xray-{instanceId}";
        var stats = await docker.GetContainerStatsAsync(mainContainerName);

        long totalBytesIn = 0;
        long totalBytesOut = 0;

        if (stats.Networks != null)
        {
            foreach (var network in stats.Networks.Values)
            {
                totalBytesIn += (long)network.RxBytes;
                totalBytesOut += (long)network.TxBytes;
            }
        }

        var traffic = new { TotalBytesIn = totalBytesIn, TotalBytesOut = totalBytesOut };
        return JsonConvert.SerializeObject(traffic);
    }
}