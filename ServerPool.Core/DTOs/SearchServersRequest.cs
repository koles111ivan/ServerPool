namespace ServerPool.Core.DTOs;

public class SearchServersRequest
{
    public string? OperatingSystem { get; set; }
    public int? MinMemoryGB { get; set; }
    public int? MinDiskGB { get; set; }
    public int? MinCpuCores { get; set; }
}
