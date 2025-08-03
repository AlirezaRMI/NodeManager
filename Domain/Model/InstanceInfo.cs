using Newtonsoft.Json;

namespace Domain.Model;

public class InstanceInfo
{
    [JsonProperty("Id")] public long Id { get; set; }
    
    [JsonProperty("LastTotalRx")] public long LastTotalRx { get; set; }
    
    [JsonProperty("LastTotalTx")] public long LastTotalTx { get; set; } 
}