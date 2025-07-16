namespace Domain.Models.Node;

public class NodeDto
{
    public long Id { get; set; }
    
    public string NodeName { get; set; } = null!;
    
    public string SshHost { get; set; } = null!;
    
    public int SshPort { get; set; }
    
    public string SshUsername { get; set; } = null!;

    public string? XrayContainerImage { get; set; }
    
    public string? AvailablePortsRangeJson { get; set; }
}