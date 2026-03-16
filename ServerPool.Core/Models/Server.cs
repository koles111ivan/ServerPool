 namespace ServerPool.Core.Models;

public class Server
{
    public Guid Id { get; set; }
    public string OperatingSystem { get; set; } = string.Empty;
    public int MemoryGB { get; set; }
    public int DiskGB { get; set; }
    public int CpuCores { get; set; }
    public ServerStatus Status { get; set; }
    public DateTime? AllocatedAt { get; set; }
    public DateTime? PowerOnRequestedAt { get; set; }
    public string? AllocatedTo { get; set; }
}

public enum ServerStatus
{
    Available,      
    Allocated,      
    PoweringOn,     
    Offline         
}
