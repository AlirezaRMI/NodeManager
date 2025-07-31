using Newtonsoft.Json;

namespace Domain.Model;

public class TrafficUsageDto
{
    [JsonProperty("total_bytes_in")]
    public long TotalBytesIn { get; set; }

    [JsonProperty("total_bytes_out")]
    public long TotalBytesOut { get; set; }  
}