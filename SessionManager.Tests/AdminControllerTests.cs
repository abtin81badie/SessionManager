using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SessionManager.Api.Controllers;
using SessionManager.Application.Common;
using SessionManager.Application.DTOs;
using SessionManager.Application.Features.Admin.GetStats;
using Xunit;

namespace SessionManager.Tests
{
    public class AdminControllerTests
    {
        private readonly Mock<IMediator> _mockMediator;
        private readonly UserSessionContext _userContext;
        private readonly AdminController _controller;

        public AdminControllerTests()
        {
            _mockMediator = new Mock<IMediator>();

            // 1. Instantiate the context directly
            _userContext = new UserSessionContext();

            // 2. Inject the real context into the Controller
            _controller = new AdminController(_mockMediator.Object, _userContext);
        }

        [Fact]
        public async Task GetSessionStats_Should_ReturnOk_When_MediatorReturnsData()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var sessionId = "test-session";
            var userRole = "Admin";

            // 1. Setup Context Data (Simulating the Middleware)
            _userContext.UserId = userId;
            _userContext.SessionId = sessionId;
            _userContext.Role = userRole;
            _userContext.IsAuthenticated = true; // Important: Middleware sets this to true

            // 2. Mock Mediator Response
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
            Assert.Single(response.DetailedSessions);
            Assert.Contains(response.Links, l => l.Rel == "self");

            // Verify Query Construction: Ensure Controller passed data from _userContext correctly
            _mockMediator.Verify(x => x.Send(It.Is<GetSessionStatsQuery>(q =>
                q.UserId == userId &&
                q.Role == userRole &&
                q.CurrentSessionId == sessionId), default), Times.Once);
        }


        [Fact]
        public async Task GetSessionStats_Should_ReturnUnauthorized_When_MediatorReturnsNull()
        {
            // Arrange
            _userContext.UserId = Guid.NewGuid();
            _userContext.Role = "User";
            _userContext.SessionId = "expired-session";
            _userContext.IsAuthenticated = true;

            // Mock Mediator returning NULL (simulating business logic failure)
            _mockMediator.Setup(x => x.Send(It.IsAny<GetSessionStatsQuery>(), default))
                .ReturnsAsync((SessionStatsDto?)null);

            // Act
            var result = await _controller.GetSessionStats();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);

            // Reflection to get the 'Message' property from the anonymous object
            var value = unauthorizedResult.Value;
            var messageProperty = value?.GetType().GetProperty("Message");
            var messageValue = messageProperty?.GetValue(value, null) as string;

            Assert.Equal("Session expired or revoked.", messageValue);
        }
    }
}