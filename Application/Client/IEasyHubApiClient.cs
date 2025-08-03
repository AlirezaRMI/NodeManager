using Domain.DTOs.Instance;

namespace Application.Client;

public interface IEasyHubApiClient
{
    Task SubmitUsageAsync(UsageReportDto report); 
}