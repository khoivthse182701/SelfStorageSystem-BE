namespace SelfStorageSystem.Contracts.Manager;

public class ManagerFacilityScopeDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string AssignmentRole { get; set; } = string.Empty;
}
