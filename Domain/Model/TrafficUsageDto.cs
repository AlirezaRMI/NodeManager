using Newtonsoft.Json;

namespace Domain.Model;

public class TrafficUsageDto
{
    [JsonProperty("TotalBytesIn")]
    public long TotalBytesIn { get; set; }

    [JsonProperty("TotalBytesOut")]
    public long TotalBytesOut { get; set; }
}