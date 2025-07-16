namespace Domain.Enumes;

public enum ProvisionStatus
{
    Pending = 0,
    Provisioning = 1,
    Running = 2,
    Stopped = 3,
    Failed = 4,
    Deleting = 5,
    Deleted = 6
}