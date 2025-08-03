using System.Net.Http.Json;
using Application.Services.Interfaces;
using Domain.DTOs.Instance;
using Domain.Models.Provision;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;


namespace Application.Services.implementations;


public class NodeService(IDockerService docker, ILogger<INodeService> logger,ILocalInstanceStore localInstanceStore) : INodeService
{
    
    private const string LocalInstanceDbPath = "/var/lib/easyhub-instance-data/instances.json";
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

        var containerId = await docker.CreateContainerAsync(
            imageName: r.XrayContainerImage,
            containerName: mainContainerName,
            portMappings: ports,
            environmentVariables: envVars,
            volumeMappings: volumes);

        await docker.StartContainerAsync(containerId);
        logger.LogInformation("Container started ({Id})", containerId);
        using var httpClient = new HttpClient();
        var portsToWatch = new[] { r.InboundPort, r.ApiPort, r.XrayPort };
        foreach (var port in portsToWatch)
        {
            await httpClient.PostAsJsonAsync("http://localhost:9191/watch", new { port });
        }
        await localInstanceStore.AddAsync(new InstanceInfo { Id = r.InstanceId });

        return new ProvisionResponseDto
        {
            ProvisionedInstanceId = r.InstanceId,
            IsSuccess = true,
            ContainerDockerId = containerId,
            XrayUserUuid = "UUID_NOT_EXTRACTED",
        };
    }

    public async Task<string> DeprovisionContainerAsync(string id,long instanceId)
    {
        await docker.StopContainerAsync(id);
        await docker.DeleteContainerAsync(id);
        await localInstanceStore.RemoveAsync(instanceId);
        return $"Container {id} removed";
    }

    public Task<string> GetContainerStatusAsync(string id) => docker.GetContainerStatusAsync(id);
    public Task<string> GetContainerLogsAsync(string id)   => docker.GetContainerLogsAsync(id);
    public Task<string> PauseContainerAsync(string id) {  docker.PauseContainerAsync(id);   return Task.FromResult($"{id} paused"); }
    public Task<string> ResumeContainerAsync(string id) { docker.UnpauseContainerAsync(id); return Task.FromResult($"{id} resumed"); }
    public async Task<IEnumerable<InstanceInfo>> GetAllLocalInstancesAsync()
    {
        if (!File.Exists(LocalInstanceDbPath))
        {
            return [];
        }
        var json = await File.ReadAllTextAsync(LocalInstanceDbPath);
        return JsonConvert.DeserializeObject<List<InstanceInfo>>(json) ?? new List<InstanceInfo>();
    }

    public async Task<string> GetInstanceTrafficAsync(long instanceId)
    {
        var mainContainerName = $"easyhub-xray-{instanceId}";
        var rx = await docker.ExecuteCommandInContainerAsync(mainContainerName, ["cat", "/sys/class/net/eth0/statistics/rx_bytes"
        ]);
        var tx = await docker.ExecuteCommandInContainerAsync(mainContainerName, ["cat", "/sys/class/net/eth0/statistics/tx_bytes"
        ]);
        var traffic = new
        {
            TotalBytesIn = long.Parse(rx.Trim()),
            TotalBytesOut = long.Parse(tx.Trim())
        };
        return JsonConvert.SerializeObject(traffic);
    }

}
