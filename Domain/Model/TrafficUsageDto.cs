using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Domain.Model;

public class TrafficUsageDto
{
    [JsonPropertyName("TotalBytesIn")]
    public long TotalBytesIn { get; set; }

    [JsonPropertyName("TotalBytesOut")]
    public long TotalBytesOut { get; set; }
}