using Application.Client;
using Application.Services.Interfaces;
using Domain.DTOs.Instance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Application.Infrastructure;

public class UsageReportingJob(IServiceProvider serviceProvider, ILogger<UsageReportingJob> logger)
    : IHostedService, IDisposable
{
    private Timer? _timer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("🟢 Usage Reporting Job is starting.");
        
        _timer = new Timer(DoWork, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        return Task.CompletedTask;
    }

    private async void DoWork(object? state)
    {
        logger.LogInformation("🔁 Usage Reporting Job triggered at {Time}", DateTime.UtcNow);
        try
        {
            using var scope = serviceProvider.CreateScope();
            var nodeService = scope.ServiceProvider.GetRequiredService<INodeService>();
            var easyHubClient = scope.ServiceProvider.GetRequiredService<IEasyHubApiClient>();
            var localInstanceStore = scope.ServiceProvider.GetRequiredService<ILocalInstanceStore>();

            
            var localInstances = await localInstanceStore.GetAllAsync();
            if (!localInstances.Any())
            {
                logger.LogWarning("⚠️ No local instances found. Skipping report submission.");
                return;
            }

            logger.LogInformation("📦 Found {Count} local instance(s).", localInstances.Count);

            var report = new UsageReportDto();

            foreach (var instance in localInstances)
            {
                logger.LogInformation("📍 Processing instance {InstanceId}", instance.Id);
                try
                {
                    
                    var trafficJson = await nodeService.GetInstanceTrafficAsync(instance.Id);
                    if (string.IsNullOrWhiteSpace(trafficJson)) continue;

                    var currentTraffic = JsonConvert.DeserializeObject<TrafficUsageDto>(trafficJson);
                    if (currentTraffic == null) continue;

                    
                    long usageDelta = 0;

                    
                    if (instance.LastTotalRx > 0 || instance.LastTotalTx > 0)
                    {
                        if (currentTraffic.TotalBytesIn >= instance.LastTotalRx &&
                            currentTraffic.TotalBytesOut >= instance.LastTotalTx)
                        {
                            var rxDelta = currentTraffic.TotalBytesIn - instance.LastTotalRx;
                            var txDelta = currentTraffic.TotalBytesOut - instance.LastTotalTx;
                            usageDelta = rxDelta + txDelta;
                        }
                    }

                    if (usageDelta > 0)
                    {
                        logger.LogInformation("📊 Usage for instance {InstanceId} in this interval: {Bytes} bytes",
                            instance.Id, usageDelta);
                        report.Usages.Add(new InstanceUsageData
                        {
                            InstanceId = instance.Id,
                            TotalUsageInBytes = usageDelta
                        });
                    }
                    else
                    {
                        logger.LogInformation("ℹ️ No new usage for instance {InstanceId} in this interval.",
                            instance.Id);
                    }
                    instance.LastTotalRx = currentTraffic.TotalBytesIn;
                    instance.LastTotalTx = currentTraffic.TotalBytesOut;

                    await localInstanceStore.UpdateAsync(instance);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "❌ Failed to get traffic for instance {InstanceId}", instance.Id);
                }
            }

            if (report.Usages.Any())
            {
                logger.LogInformation("📤 Submitting usage report with {Count} usage item(s) to EasyHub.",
                    report.Usages.Count);
                await easyHubClient.SubmitUsageAsync(report);
                logger.LogInformation("✅ Successfully submitted usage report.");
            }
            else
            {
                logger.LogWarning("⚠️ No usage to report in this cycle.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❗ A critical error occurred in the usage reporting job.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("🛑 Usage Reporting Job is stopping.");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
        logger.LogInformation("🔧 Usage Reporting Job disposed.");
    }
}