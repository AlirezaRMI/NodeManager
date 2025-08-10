using Newtonsoft.Json;

namespace Domain.Model;

public class InstanceInfo
{
    public long Id { get; set; }
    public int InboundPort { get; set; }
}