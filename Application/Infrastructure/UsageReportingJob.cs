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
        _timer = new Timer(DoWork, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2));
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

            var localInstances = await nodeService.GetAllLocalInstancesAsync();
            var instanceInfos = localInstances as InstanceInfo[] ?? localInstances.ToArray();
            logger.LogInformation("📦 Found {Count} local instance(s).", instanceInfos.Length);

            if (!instanceInfos.Any())
            {
                logger.LogWarning("⚠️ No local instances found. Skipping report submission.");
                return;
            }

            var report = new UsageReportDto();

            foreach (var instance in instanceInfos)
            {
                logger.LogInformation("📍 Processing instance {InstanceId}", instance.Id);

                try
                {
                    var trafficJson = await nodeService.GetInstanceTrafficAsync(instance.Id);

                    if (string.IsNullOrWhiteSpace(trafficJson))
                    {
                        logger.LogWarning("⚠️ Empty traffic data for instance {InstanceId}", instance.Id);
                        continue;
                    }

                    var trafficData = JsonConvert.DeserializeObject<TrafficUsageDto>(trafficJson);
                    if (trafficData == null)
                    {
                        logger.LogWarning("⚠️ Failed to deserialize traffic data for instance {InstanceId}",
                            instance.Id);
                        continue;
                    }

                    var totalUsage = trafficData.TotalBytesIn + trafficData.TotalBytesOut;
                    logger.LogInformation("📊 Total usage for instance {InstanceId}: {Bytes} bytes", instance.Id,
                        totalUsage);

                    report.Usages.Add(new InstanceUsageData
                    {
                        InstanceId = instance.Id,
                        TotalUsageInBytes = totalUsage
                    });
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
                logger.LogWarning("⚠️ Usage report is empty — nothing to submit.");
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