using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ServerPool.API.Controllers;
using ServerPool.Core.DTOs;
using ServerPool.Core.Interfaces;
using ServerPool.Core.Models;
using Xunit;

namespace ServerPool.Tests.Controllers;

public class ServersControllerTests
{
    private readonly Mock<IServerService> _mockServerService;
    private readonly Mock<ILogger<ServersController>> _mockLogger;
    private readonly ServersController _controller;

    public ServersControllerTests()
    {
        _mockServerService = new Mock<IServerService>();
        _mockLogger = new Mock<ILogger<ServersController>>();
        _controller = new ServersController(_mockServerService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task AddServer_ShouldReturnCreatedResult()
    {
        // Arrange
        var request = new AddServerRequest
        {
            OperatingSystem = "Windows Server 2022",
            MemoryGB = 32,
            DiskGB = 500,
            CpuCores = 8,
            IsOnline = true
        };

        var server = new Server
        {
            Id = Guid.NewGuid(),
            OperatingSystem = request.OperatingSystem,
            MemoryGB = request.MemoryGB,
            DiskGB = request.DiskGB,
            CpuCores = request.CpuCores,
            Status = ServerStatus.Available
        };

        _mockServerService.Setup(s => s.AddServerAsync(request))
            .ReturnsAsync(server);

        // Act
        var result = await _controller.AddServer(request);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task AllocateServer_ShouldReturnOkResult()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        var request = new AllocateServerRequest
        {
            ServerId = serverId,
            AllocatedTo = "user1"
        };

        var server = new Server
        {
            Id = serverId,
            OperatingSystem = "Windows Server 2022",
            MemoryGB = 32,
            DiskGB = 500,
            CpuCores = 8,
            Status = ServerStatus.Allocated,
            AllocatedTo = "user1",
            AllocatedAt = DateTime.UtcNow
        };

        _mockServerService.Setup(s => s.AllocateServerAsync(serverId, "user1"))
            .ReturnsAsync(server);

        // Act
        var result = await _controller.AllocateServer(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task AllocateServer_WhenServerNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        var request = new AllocateServerRequest
        {
            ServerId = serverId,
            AllocatedTo = "user1"
        };

        _mockServerService.Setup(s => s.AllocateServerAsync(serverId, "user1"))
            .ReturnsAsync((Server?)null);

        // Act
        var result = await _controller.AllocateServer(request);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ReleaseServer_ShouldReturnNoContent()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        _mockServerService.Setup(s => s.ReleaseServerAsync(serverId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.ReleaseServer(serverId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task IsServerReady_ShouldReturnOkWithStatus()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        var server = new Server
        {
            Id = serverId,
            OperatingSystem = "Windows Server 2022",
            MemoryGB = 32,
            DiskGB = 500,
            CpuCores = 8,
            Status = ServerStatus.Available
        };

        _mockServerService.Setup(s => s.GetServerByIdAsync(serverId))
            .ReturnsAsync(server);
        _mockServerService.Setup(s => s.IsServerReadyAsync(serverId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.IsServerReady(serverId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }
}
