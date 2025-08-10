using Application.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Application.Services.implementations;

public class PtablesRuleRestorerService(IServiceProvider provider,ILogger<PtablesRuleRestorerService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Restoring iptables traffic rules on startup...");
        await Task.Delay(10000, cancellationToken); 

        using var scope = provider.CreateScope();
        var localInstanceStore = scope.ServiceProvider.GetRequiredService<ILocalInstanceStore>();
        var dockerService = scope.ServiceProvider.GetRequiredService<IDockerService>();

        var instances = await localInstanceStore.GetAllAsync();
        logger.LogInformation("Found {Count} instances to restore rules for.", instances.Count);

        foreach (var instance in instances)
        {
            try
            {
                logger.LogInformation("Restoring rule for port {Port}", instance.InboundPort);
                await dockerService.AddTrafficCountingRuleAsync(instance.InboundPort);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to restore iptables rule for port {Port}", instance.InboundPort);
            }
        }
        logger.LogInformation("Iptables rule restoration complete.");
    }

    public Task StopAsync(CancellationToken cancellationToken)=>Task.CompletedTask;
 
}