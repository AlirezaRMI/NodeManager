using Application.Services.Interfaces;
using Domain.Models.Provision;
using Microsoft.Extensions.Logging;

namespace Application.Services.implementations;

public class NodeService(IDockerService docker, ILogger<INodeService> logger) : INodeService
{
    public async Task<ProvisionResponseDto> ProvisionContainerAsync(ProvisionRequestDto r)
    {
        logger.LogInformation("Provision request for Instance {Id}", r.InstanceId);
        if (r.InboundPort <= 0 || r.XrayPort <= 0 || r.ApiPort <= 0)
            throw new ArgumentException("All requested ports must be > 0");

        var baseDir = $"/var/lib/easyhub-instance-data/{r.InstanceId}";
        var sslDir  = Path.Combine(baseDir, "ssl");
        var cert    = Path.Combine(sslDir, "ssl_client_cert.pem");
        var key     = Path.Combine(sslDir, "ssl_client_key.pem");

        await docker.CreateDirectoryOnHostAsync(sslDir);
        await docker.WriteFileOnHostAsync(cert, r.SshPrivateKey!);
        await docker.WriteFileOnHostAsync(key,  r.SshPrivateKey!);

        await docker.OpenFirewallPortAsync(r.InboundPort);
        await docker.OpenFirewallPortAsync(r.XrayPort);
        await docker.OpenFirewallPortAsync(r.ApiPort);

        var envVars = new Dictionary<string,string>
        {
            ["SERVICE_PORT"]        = r.ApiPort.ToString(), 
            ["XRAY_API_PORT"]       = r.XrayPort.ToString(),   
            ["SERVICE_PROTOCOL"]    = "rest",
            ["SSL_CLIENT_CERT_FILE"] = "/var/lib/marzban-node/ssl/ssl_client_cert.pem",
            ["SSL_CLIENT_KEY_FILE"]  = "/var/lib/marzban-node/ssl/ssl_client_key.pem"
        };

        var volumes = new List<string>
        {
            $"{sslDir}:/var/lib/marzban-node/ssl:ro"
        };
        var ports   = new List<string>
        {
            $"{r.InboundPort}:443/tcp",
            $"{r.XrayPort}:8080/tcp",
            $"{r.ApiPort}:8484/tcp"
        };

        var containerId = await docker.CreateContainerAsync(
            imageName      : r.XrayContainerImage,
            containerName  : $"easyhub-xray-{r.InstanceId}",
            portMappings   : ports,
            environmentVariables : envVars,
            volumeMappings : volumes);

        await docker.StartContainerAsync(containerId);
        logger.LogInformation("Container started ({Id})", containerId);

        return new ProvisionResponseDto
        {
            ProvisionedInstanceId = r.InstanceId,
            IsSuccess            = true,
            ContainerDockerId    = containerId,
            XrayUserUuid         = "UUID_NOT_EXTRACTED"
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
    public Task<string> PauseContainerAsync(string id)     {  docker.PauseContainerAsync(id);   return Task.FromResult($"{id} paused"); }

    public Task<string> ResumeContainerAsync(string id)
    {
        docker.UnpauseContainerAsync(id); return Task.FromResult($"{id} resumed");
    }
}
