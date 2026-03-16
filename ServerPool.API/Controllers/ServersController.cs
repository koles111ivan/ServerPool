using Microsoft.AspNetCore.Mvc;
using ServerPool.Core.DTOs;
using ServerPool.Core.Interfaces;
using ServerPool.Core.Models;

namespace ServerPool.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServersController : ControllerBase
{
    private readonly IServerService _serverService;
    private readonly ILogger<ServersController> _logger;

    public ServersController(IServerService serverService, ILogger<ServersController> logger)
    {
        _serverService = serverService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ServerResponse>>> GetAllServers()
    {
        var servers = await _serverService.GetAllServersAsync();
        return Ok(servers.Select(MapToResponse));
    }

    [HttpPost]
    public async Task<ActionResult<ServerResponse>> AddServer([FromBody] AddServerRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var server = await _serverService.AddServerAsync(request);
        return CreatedAtAction(nameof(GetServerById), new { id = server.Id }, MapToResponse(server));
    }

    [HttpPost("search")]
    public async Task<ActionResult<IEnumerable<ServerResponse>>> SearchServers([FromBody] SearchServersRequest request)
    {
        var servers = await _serverService.SearchAvailableServersAsync(request);
        return Ok(servers.Select(MapToResponse));
    }

    [HttpPost("allocate")]
    public async Task<ActionResult<ServerResponse>> AllocateServer([FromBody] AllocateServerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AllocatedTo))
        {
            return BadRequest("AllocatedTo is required");
        }

        var server = await _serverService.AllocateServerAsync(request.ServerId, request.AllocatedTo);
        
        if (server == null)
        {
            return NotFound("Server not found or not available");
        }

        return Ok(MapToResponse(server));
    }

    [HttpPost("{id}/release")]
    public async Task<ActionResult> ReleaseServer(Guid id)
    {
        var result = await _serverService.ReleaseServerAsync(id);
        
        if (!result)
        {
            return NotFound("Server not found or not allocated");
        }

        return NoContent();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ServerResponse>> GetServerById(Guid id)
    {
        var server = await _serverService.GetServerByIdAsync(id);
        
        if (server == null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(server));
    }

    [HttpGet("{id}/ready")]
    public async Task<ActionResult<object>> IsServerReady(Guid id)
    {
        var server = await _serverService.GetServerByIdAsync(id);
        
        if (server == null)
        {
            return NotFound();
        }

        var isReady = await _serverService.IsServerReadyAsync(id);
        var estimatedReadyAt = server.Status == ServerStatus.PoweringOn && 
                               server.PowerOnRequestedAt.HasValue
            ? server.PowerOnRequestedAt.Value.AddMinutes(5)
            : (DateTime?)null;

        return Ok(new
        {
            IsReady = isReady,
            Status = server.Status.ToString(),
            EstimatedReadyAt = estimatedReadyAt
        });
    }

    private ServerResponse MapToResponse(Server server)
    {
        var isReady = server.Status == ServerStatus.Available ||
                     (server.Status == ServerStatus.PoweringOn &&
                      server.PowerOnRequestedAt.HasValue &&
                      DateTime.UtcNow >= server.PowerOnRequestedAt.Value.AddMinutes(5));

        var estimatedReadyAt = server.Status == ServerStatus.PoweringOn &&
                              server.PowerOnRequestedAt.HasValue
            ? server.PowerOnRequestedAt.Value.AddMinutes(5)
            : (DateTime?)null;

        return new ServerResponse
        {
            Id = server.Id,
            OperatingSystem = server.OperatingSystem,
            MemoryGB = server.MemoryGB,
            DiskGB = server.DiskGB,
            CpuCores = server.CpuCores,
            Status = server.Status.ToString(),
            AllocatedAt = server.AllocatedAt,
            AllocatedTo = server.AllocatedTo,
            IsReady = isReady,
            EstimatedReadyAt = estimatedReadyAt
        };
    }
}
