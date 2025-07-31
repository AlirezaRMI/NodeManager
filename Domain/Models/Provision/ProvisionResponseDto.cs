namespace Domain.Models.Provision;

public class ProvisionResponseDto
{
    public long ProvisionedInstanceId { get; set; } 
    public bool IsSuccess { get; set; }
    
    public string? ErrorMessage { get; set; } 
    public string? ContainerDockerId { get; set; }
    public string? XrayUserUuid { get; set; } 
    
    public string? GeneratedXrayConfigJson { get; set; } 
}