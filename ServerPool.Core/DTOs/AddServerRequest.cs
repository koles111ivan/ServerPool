namespace ServerPool.Core.DTOs;

public class AddServerRequest
{
    public string OperatingSystem { get; set; } = string.Empty;
    public int MemoryGB { get; set; }
    public int DiskGB { get; set; }
    public int CpuCores { get; set; }
    public bool IsOnline { get; set; } = true;
}
