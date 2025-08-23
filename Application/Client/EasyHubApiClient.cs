using System.Net.Http.Headers;
using System.Net.Http.Json;
using Domain.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Client;

public class EasyHubApiClient(
    ILogger<IEasyHubApiClient> logger,
    HttpClient httpClient,
    IOptions<EasyhubTemplateModel> options) : IEasyHubApiClient
{
 
    public async Task SubmitUsageAsync(UsageReportDto report,string? apiKey)
    {
        
        httpClient.BaseAddress = new Uri(options.Value.Url!.TrimEnd('/'));
        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);


        var requestUrl = EasyHubUrlPath.UpdateUsage;
        logger.LogInformation("Submitting usage report to EasyHub at {Url}", requestUrl);

        logger.LogInformation("EasyHub BaseAddress = {BaseAddress}", httpClient.BaseAddress);
        try
        {
            var jsonContent = JsonContent.Create(report);

            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = jsonContent
            };
            var response = await httpClient.SendAsync(request);
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