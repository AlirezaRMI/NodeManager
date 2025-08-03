using Domain.Model;

namespace Application.Client;

public interface IEasyHubApiClient
{
    Task SubmitUsageAsync(UsageReportDto report); 
}