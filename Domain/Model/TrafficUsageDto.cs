using Newtonsoft.Json;

namespace Domain.DTOs.Instance;

public class TrafficUsageDto
{
    [JsonProperty("in")]
    public long TotalBytesIn { get; set; }

    [JsonProperty("out")]
    public long TotalBytesOut { get; set; }
}