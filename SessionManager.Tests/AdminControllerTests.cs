using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SessionManager.Api.Controllers;
using SessionManager.Application.DTOs;
using SessionManager.Application.Features.Admin.GetStats;
using SessionManager.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace SessionManager.Tests
{
    public class AdminControllerTests
    {
        private readonly Mock<IMediator> _mockMediator;
        private readonly Mock<ICurrentUserService> _mockUserService;
        private readonly AdminController _controller;

        public AdminControllerTests()
        {
            _mockMediator = new Mock<IMediator>();
            _mockUserService = new Mock<ICurrentUserService>();

            _controller = new AdminController(_mockMediator.Object, _mockUserService.Object);
        }

        [Fact]
        public async Task GetSessionStats_Should_ReturnOk_When_MediatorReturnsData()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var sessionId = "test-session";
            var userRole = "Admin";

            // 1. Mock the Service (Controller reads from here)
            _mockUserService.Setup(x => x.UserId).Returns(userId);
            _mockUserService.Setup(x => x.SessionId).Returns(sessionId);
            _mockUserService.Setup(x => x.Role).Returns(userRole);

            // 2. Mock Mediator Response
            // FIX: Using 'SessionDetailDto' to match your class definition
            var statsDto = new SessionStatsDto
            {
                TotalActiveSessions = 5,
                UsersOnline = 2,
                DetailedSessions = new List<SessionDetailDto>
                {
                    new SessionDetailDto
                    {
                        UserId = Guid.NewGuid(),
                        UserName = "TestUser",
                        DeviceInfo = "Chrome",
                        Token = "token-123",
                        Role = "User",
                        IsCurrentSession = false
                    }
                }
            };

            _mockMediator.Setup(x => x.Send(It.IsAny<GetSessionStatsQuery>(), default))
                .ReturnsAsync(statsDto);

            // Act
            var result = await _controller.GetSessionStats();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<SessionStatsResponse>(okResult.Value);

            Assert.Equal(5, response.TotalActiveSessions);
            Assert.Single(response.DetailedSessions); // Ensure the list has 1 item
            Assert.Contains(response.Links, l => l.Rel == "self"); // Verify HATEOAS was added

            // Verify Query Construction
            _mockMediator.Verify(x => x.Send(It.Is<GetSessionStatsQuery>(q =>
                q.UserId == userId &&
                q.Role == userRole &&
                q.CurrentSessionId == sessionId), default), Times.Once);
        }


        [Fact]
        public async Task GetSessionStats_Should_ReturnUnauthorized_When_MediatorReturnsNull()
        {
            // Arrange
            _mockUserService.Setup(x => x.UserId).Returns(Guid.NewGuid());
            _mockUserService.Setup(x => x.Role).Returns("User");
            _mockUserService.Setup(x => x.SessionId).Returns("expired-session");

            // Mock Mediator returning NULL
            _mockMediator.Setup(x => x.Send(It.IsAny<GetSessionStatsQuery>(), default))
                .ReturnsAsync((SessionStatsDto)null);

            // Act
            var result = await _controller.GetSessionStats();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);

            // FIX: Use Reflection to get the 'Message' property from the anonymous object
            var value = unauthorizedResult.Value;
            var messageProperty = value.GetType().GetProperty("Message");
            var messageValue = messageProperty?.GetValue(value, null) as string;

            Assert.Equal("Session expired or revoked.", messageValue);
        }
    }
}