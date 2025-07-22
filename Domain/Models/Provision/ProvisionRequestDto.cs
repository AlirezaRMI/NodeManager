namespace Domain.Models.Provision;

public class ProvisionRequestDto
{
    public long InstanceId { get; set; } 
    public string XrayContainerImage { get; set; } = null!;
    public long CustomerId { get; set; }
    
}