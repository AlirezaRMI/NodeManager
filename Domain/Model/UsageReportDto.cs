namespace Domain.Model;

public class UsageReportDto
{
    public List<InstanceUsageData> Usages { get; set; } = new();
}