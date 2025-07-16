namespace Domain.Models.Provision;

public class ProvisionResponseDto
{
    public long ProvisionedInstanceId { get; set; } 
    
    public bool IsSuccess { get; set; }
    
    public string? ErrorMessage { get; set; } 

    public string? ContainerDockerId { get; set; } 
        
    public int AssignedInboundPort { get; set; } 
    
    public int AssignedXrayPort { get; set; } 
    
    public int AssignedServerPort { get; set; } 

    public string? XrayUserUuid { get; set; } 
    
    public string? GeneratedXrayConfigJson { get; set; } 
}