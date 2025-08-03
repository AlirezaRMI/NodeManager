namespace Domain.DTOs.Instance;

public class UsageReportDto
{
    public List<InstanceUsageData> Usages { get; set; } = new();
}