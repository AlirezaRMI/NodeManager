using Application.Client;
using Application.Services.Interfaces;
using Domain.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class UsageReportingJob(IServiceProvider serviceProvider, ILogger<UsageReportingJob> logger)
    : IHostedService, IDisposable
{
    private Timer? _timer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(DoWork, null, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(10));
        return Task.CompletedTask;
    }

    private async void DoWork(object? state)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var nodeService = scope.ServiceProvider.GetRequiredService<INodeService>();
            var easyHubClient = scope.ServiceProvider.GetRequiredService<IEasyHubApiClient>();

            var localInstances = await nodeService.GetAllLocalInstancesAsync();
            if (!localInstances.Any()) return;

            var report = new UsageReportDto();
            foreach (var instance in localInstances)
            {
                try
                {
                    var trafficJson = await nodeService.GetInstanceTrafficAsync(instance.Id);
                    var currentTraffic = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(trafficJson)!;
                    long totalUsage = currentTraffic.TotalBytesIn + currentTraffic.TotalBytesOut;

                    if (totalUsage > 0)
                    {
                        report.Usages.Add(new InstanceUsageData
                        {
                            InstanceId = instance.Id,
                            TotalUsageInBytes = totalUsage
                        });
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to get traffic for instance {InstanceId}", instance.Id);
                }
            }

            if (report.Usages.Any())
            {
                await easyHubClient.SubmitUsageAsync(report);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "A critical error occurred in the usage reporting job.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}