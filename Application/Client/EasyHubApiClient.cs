using System.Net.Http.Headers;
using System.Net.Http.Json;
using Domain.DTOs.Instance;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Client;

public class EasyHubApiClient(
    ILogger<IEasyHubApiClient> logger,
    HttpClient httpClient,
    IOptions<EasyhubTemplateModel> options) : IEasyHubApiClient
{
    public async Task SubmitUsageAsync(UsageReportDto report)
    {
        httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        httpClient.BaseAddress = new Uri(options.Value.Url!);

        var requestUrl = EasyHubUrlPath.UpdateUsage;
        logger.LogInformation("Submitting usage report to EasyHub at {Url}", requestUrl);

        try
        {
            var response = await httpClient.PostAsJsonAsync(requestUrl, report);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                logger.LogError("Failed to submit usage report to EasyHub. Status: {StatusCode}, Details: {Details}",
                    response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An exception occurred while submitting usage report to EasyHub at {Url}", requestUrl);
        }
    }
}