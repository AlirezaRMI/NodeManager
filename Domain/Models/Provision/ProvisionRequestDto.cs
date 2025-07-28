namespace Domain.Models.Provision;

public class ProvisionRequestDto
{
    public long InstanceId { get; set; } 
    
    public string SshPrivateKey { get; set; } = null!;
    
    public string XrayContainerImage { get; set; } = null!;

    public long CustomerId { get; set; }

    public int InboundPort { get; set; }

    public int XrayPort { get; set; }

    public int ApiPort { get; set; }
    
}