namespace ServerPool.Core.DTOs;

public class ServerResponse
{
    public Guid Id { get; set; }
    public string OperatingSystem { get; set; } = string.Empty;
    public int MemoryGB { get; set; }
    public int DiskGB { get; set; }
    public int CpuCores { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? AllocatedAt { get; set; }
    public string? AllocatedTo { get; set; }
    public bool IsReady { get; set; }
    public DateTime? EstimatedReadyAt { get; set; }
}
