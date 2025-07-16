namespace Domain.Models.Provision;

public class ProvisionRequestDto
{
    public long InstanceId { get; set; } 

    public string SshHost { get; set; } = null!;

    public int SshPort { get; set; }

    public string SshUsername { get; set; } = null!;

    public string SshPrivateKey { get; set; } = null!;

    public string? SshPassword { get; set; }

    public string XrayContainerImage { get; set; } = null!;

    public long CustomerId { get; set; }

    public int? InboundPort { get; set; }

    public int? XrayPort { get; set; }

    public int? ServerPort { get; set; }
    
}