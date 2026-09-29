namespace SelfStorageSystem.Contracts.Customer.Rentals;

public class ChangePinResponseDto
{
    public bool Success { get; set; }
    public string SyncStatus { get; set; } = null!;
    public string Message { get; set; } = null!;
    public int EstimatedSyncSeconds { get; set; }
}
