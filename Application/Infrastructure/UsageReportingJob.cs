using Application.Client;
using Application.Services.Interfaces;
using Domain.DTOs.Instance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Application.Infrastructure;

public class UsageReportingJob(IServiceProvider serviceProvider, ILogger<UsageReportingJob> logger) : IHostedService, IDisposable
{
    private Timer? _timer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Usage Reporting Job is starting.");
        _timer = new Timer(DoWork, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2));
        return Task.CompletedTask;
    }

    private void DoWork(object? state)
    {
        logger.LogInformation("Usage Reporting Job is working.");
        try
        {
            using var scope = serviceProvider.CreateScope();
            var nodeService = scope.ServiceProvider.GetRequiredService<INodeService>();
            var easyHubClient = scope.ServiceProvider.GetRequiredService<IEasyHubApiClient>();
            
            var localInstances = nodeService.GetAllLocalInstancesAsync().GetAwaiter().GetResult();
            var instanceInfos = localInstances as InstanceInfo[] ?? localInstances.ToArray();
            if (!instanceInfos.Any()) return;

            var report = new UsageReportDto();
            foreach (var instance in instanceInfos)
            {
                try
                {
                    var trafficJson = nodeService.GetInstanceTrafficAsync(instance.Id).GetAwaiter().GetResult();
                    if (string.IsNullOrWhiteSpace(trafficJson)) continue;

                    var trafficData = JsonConvert.DeserializeObject<Dictionary<string, TrafficUsageDto>>(trafficJson);
                    
                    long totalUsage = trafficData!.Values.Sum(v => v.TotalBytesIn + v.TotalBytesOut);

                    report.Usages.Add(new InstanceUsageData 
                    {
                        InstanceId = instance.Id,
                        TotalUsageInBytes = totalUsage
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to get traffic for instance {InstanceId} during reporting job.", instance.Id);
                }
            }
            
            if (report.Usages.Any())
            {
                easyHubClient.SubmitUsageAsync(report).GetAwaiter().GetResult();
                logger.LogInformation("Successfully submitted usage report for {Count} instances.", report.Usages.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "A critical error occurred in the usage reporting job.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Usage Reporting Job is stopping.");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}