namespace ServerPool.Core.DTOs;

public class AllocateServerRequest
{
    public Guid ServerId { get; set; }
    public string AllocatedTo { get; set; } = string.Empty;
}
