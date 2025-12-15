using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SessionManager.Api.Controllers; 
using SessionManager.Application.Common;
using SessionManager.Application.DTOs;
using SessionManager.Application.Features.Sessions.GetActive;
using SessionManager.Application.Features.Sessions.Renew;
using Xunit;

namespace SessionManager.Tests
{
    public class SessionsControllerTests
    {
        private readonly Mock<IMediator> _mockMediator;
        // CHANGE: Use the real object, no Mock needed
        private readonly UserSessionContext _userContext;
        private readonly SessionsController _controller;

        public SessionsControllerTests()
        {
            _mockMediator = new Mock<IMediator>();

            // 1. Instantiate the context directly
            _userContext = new UserSessionContext();

            // 2. Inject the real context
            _controller = new SessionsController(_mockMediator.Object, _userContext);
        }

        // --- HELPER: Setup Header for "Echo" Response ---
        // The controller reads Request.Headers["Authorization"] just to display it back to the user.
        private void SetupRequestHeader(string token)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Authorization"] = $"Bearer {token}";
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        // ==========================================
        // RENEW SESSION TESTS
        // ==========================================

        [Fact]
        public async Task RenewSession_Should_ReturnOk_When_MediatorReturnsTrue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var sessionId = Guid.NewGuid().ToString();
            var tokenString = "valid.jwt.token";

            // 1. Setup Context Data (Simulating the Middleware)
            _userContext.UserId = userId;
            _userContext.SessionId = sessionId;
            _userContext.IsAuthenticated = true;

            // 2. Setup Header (for the response echo only)
            SetupRequestHeader(tokenString);

            // 3. Mock Mediator Success
            _mockMediator.Setup(x => x.Send(It.IsAny<RenewSessionCommand>(), default))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.RenewSession();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<RenewSessionResponse>(okResult.Value);

            Assert.Equal("Session renewed successfully.", response.Message);
            Assert.Equal(tokenString, response.Token); // Verify it extracted the header string

            // Verify Mediator was called with correct IDs from Context
            _mockMediator.Verify(x => x.Send(It.Is<RenewSessionCommand>(c =>
                c.UserId == userId &&
                c.SessionId == sessionId), default), Times.Once);
        }

        [Fact]
        public async Task RenewSession_Should_ReturnNotFound_When_MediatorReturnsFalse()
        {
            // Arrange
            _userContext.UserId = Guid.NewGuid();
            _userContext.SessionId = "expired-session";
            _userContext.IsAuthenticated = true;

            SetupRequestHeader("token");

            // Mock Mediator Failure (Session not found or expired)
            _mockMediator.Setup(x => x.Send(It.IsAny<RenewSessionCommand>(), default))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.RenewSession();

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        // ==========================================
        // GET ACTIVE SESSIONS TESTS
        // ==========================================

        [Fact]
        public async Task GetActiveSessions_Should_ReturnList_When_SessionIsValid()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var currentSessionId = "session-1";

            _userContext.UserId = userId;
            _userContext.SessionId = currentSessionId;
            _userContext.IsAuthenticated = true;

            // Mock Data returned from Mediator
            var sessionsList = new List<SessionDto>
            {
                new SessionDto { Token = "session-1", IsCurrentSession = true, DeviceInfo = "PC" },
                new SessionDto { Token = "session-2", IsCurrentSession = false, DeviceInfo = "Mobile" }
            };

            _mockMediator.Setup(x => x.Send(It.IsAny<GetActiveSessionsQuery>(), default))
                .ReturnsAsync(sessionsList);

            // Act
            var result = await _controller.GetActiveSessions();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedSessions = Assert.IsAssignableFrom<IEnumerable<SessionDto>>(okResult.Value);

            Assert.Equal(2, returnedSessions.Count());

            // Verify HATEOAS Logic (Controller adds links)
            var current = returnedSessions.First(s => s.IsCurrentSession);
            Assert.Contains(current.Links, l => l.Rel == "logout"); // Should have logout link
        }

        [Fact]
        public async Task GetActiveSessions_Should_ReturnUnauthorized_When_MediatorReturnsNull()
        {
            // Arrange
            _userContext.UserId = Guid.NewGuid();
            _userContext.SessionId = "revoked-session";
            _userContext.IsAuthenticated = true;

            // Mock Mediator returning null (Indicates the current session is invalid/revoked)
            _mockMediator.Setup(x => x.Send(It.IsAny<GetActiveSessionsQuery>(), default))
                .ReturnsAsync((IEnumerable<SessionDto>?)null);

            // Act
            var result = await _controller.GetActiveSessions();

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }
    }
}