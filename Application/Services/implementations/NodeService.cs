using Application.Services.Interfaces;
using Domain.Models.Provision;
using Microsoft.Extensions.Logging;

namespace Application.Services.implementations;

public class NodeService(IDockerService dockerManager, ILogger<INodeService> logger) : INodeService
{
    
    public async Task<ProvisionResponseDto> ProvisionContainerAsync(ProvisionRequestDto request)
    {
        logger.LogInformation("Starting provisioning process for instance ID: {InstanceId}", request.InstanceId);

        string containerName = $"easyhub-xray-{request.InstanceId}";
        int assignedInboundPort = request.InboundPort; 
        int assignedXrayPort = request.XrayPort;      
        int assignedServerPort = request.ApiPort;  
        string containerDockerId;
        string xrayUserUuid = "UUID_NOT_EXTRACTED";
        
        string instanceSpecificDataDirOnHost = $"/var/lib/easyhub-instance-data/{request.InstanceId}";
        string instanceSslDataPathOnHost = Path.Combine(instanceSpecificDataDirOnHost, "ssl"); 


        string clientCertFilePathOnHost = Path.Combine(instanceSslDataPathOnHost, "ssl_client_cert.pem");
        string clientKeyFilePathOnHost = Path.Combine(instanceSslDataPathOnHost, "ssl_client_key.pem");
        
            await dockerManager.CreateDirectoryOnHostAsync(instanceSslDataPathOnHost);
            logger.LogInformation("Directory created on host: {Path}", instanceSslDataPathOnHost);
            
            if (!string.IsNullOrEmpty(request.SshPrivateKey))
            {
                await dockerManager.WriteFileOnHostAsync(clientCertFilePathOnHost, request.SshPrivateKey);
                logger.LogInformation("Client cert written to host: {Path}", clientCertFilePathOnHost);
            }
            else
            {
                logger.LogWarning("SslCertificateContent is null or empty. Client cert file will not be written.");
            }

            if (!string.IsNullOrEmpty(request.SshPrivateKey))
            {
                await dockerManager.WriteFileOnHostAsync(clientKeyFilePathOnHost, request.SshPrivateKey);
                logger.LogInformation("Client key written to host: {Path}", clientKeyFilePathOnHost);
            }
            else
            {
                logger.LogWarning("SslKeyContent is null or empty. Client key file will not be written.");
            }
       
            
            await dockerManager.OpenFirewallPortAsync(assignedInboundPort);
            await dockerManager.OpenFirewallPortAsync(assignedXrayPort);
            await dockerManager.OpenFirewallPortAsync(assignedServerPort);
            logger.LogInformation("Firewall ports opened: {Inbound}, {Xray}, {Server}", assignedInboundPort, assignedXrayPort, assignedServerPort);
            
            var envVars = new Dictionary<string, string>
            {
                { "SERVICE_PORT", assignedInboundPort.ToString() }, 
                { "XRAY_API_PORT", assignedXrayPort.ToString() },
                { "SERVICE_PROTOCOL", "rest" },
                { "SSL_CLIENT_CERT_FILE", "/var/lib/marzban-node/ssl_client_cert.pem" },
            };
            
            var volumes = new List<string>
            {
                $"{clientCertFilePathOnHost}:/var/lib/marzban-node/ssl_client_cert.pem",
                $"{clientKeyFilePathOnHost}:/var/lib/marzban-node/ssl_client_key.pem",
            };

         
            containerDockerId = await dockerManager.CreateContainerAsync(
                imageName: request.XrayContainerImage,
                containerName: containerName,
                portMappings: new List<string> { 
                    $"{assignedInboundPort}:443/tcp",
                    $"{assignedXrayPort}:8080/tcp",   
                    $"{assignedServerPort}:443/tcp"  
                }, 
                environmentVariables: envVars,
                volumeMappings: volumes
            );
            await dockerManager.StartContainerAsync(containerDockerId);
            logger.LogInformation("Container '{Name}' created and started with ID: {Id}", containerName, containerDockerId);
      
            
        return new ProvisionResponseDto
        {
            ProvisionedInstanceId = request.InstanceId,
            IsSuccess = true,
            ContainerDockerId = containerDockerId,
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
        await dockerManager.UnpauseContainerAsync(containerId);
        return $"Container {containerId} resumed.";
    }
}