using System.Text.RegularExpressions;
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
            $"{r.InboundPort}:{r.InboundPort}", $"{r.XrayPort}:62051", $"{r.ApiPort}:62050"
        };
        var mainContainerName = $"easyhub-xray-{r.InstanceId}";

        var containerId = await docker.CreateContainerAsync(
            imageName: r.XrayContainerImage,
            containerName: mainContainerName,
            portMappings: ports,
            environmentVariables: envVars,
            volumeMappings: volumes,
            networkMode:null);

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

        await docker.StopContainerAsync(id);
        await docker.DeleteContainerAsync(id);
        await localInstanceStore.RemoveAsync(instanceId);
        return $"Container {id} removed";
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