using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SessionManager.Api.Controllers;
using SessionManager.Application.DTOs;
using SessionManager.Application.Features.Auth.Login;
using SessionManager.Application.Features.Auth.Logout;
using SessionManager.Application.Interfaces;
using SessionManager.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Xunit;

namespace SessionManager.Tests
{
    public class AuthControllerTests
    {
        private readonly Mock<IMediator> _mockMediator;
        private readonly AuthController _controller;
        private readonly Mock<ICurrentUserService> _mockUserService;

        public AuthControllerTests()
        {
            _mockMediator = new Mock<IMediator>();
            _mockUserService = new Mock<ICurrentUserService>(); 
            _controller = new AuthController(_mockMediator.Object, _mockUserService.Object);
        }

        // --- HELPER: Generate Fake JWT (Needed for Logout extraction) ---
        private string GenerateTestJwt(string? jti = null, string? sub = null)
        {
            var claims = new List<Claim> { new Claim("role", "User") };

            if (jti != null) claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));
            if (sub != null) claims.Add(new Claim(JwtRegisteredClaimNames.Sub, sub));

            var token = new JwtSecurityToken(claims: claims);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // --- HELPER: Setup HttpContext with Header ---
        private void SetupHttpContext(string token)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Authorization"] = $"Bearer {token}";
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        // ==========================================
        // LOGIN TESTS (Mediator Dispatch)
        // ==========================================

        [Fact]
        public async Task Login_Should_DispatchCommand_And_ReturnOk()
        {
            // Arrange
            var request = new LoginRequest { Username = "testuser", Password = "Password123", DeviceName = "PC" };

            // Simulate the Handler returning a successful result
            var expectedResult = new LoginResult
            {
                Token = "fake-jwt",
                User = new User { Id = Guid.NewGuid(), Username = "testuser" }
            };

            _mockMediator.Setup(x => x.Send(It.IsAny<LoginCommand>(), default))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<LoginResponse>(okResult.Value);

            // Verify Mapping
            Assert.Equal("fake-jwt", response.Token);
            Assert.Contains(response.Links, l => l.Rel == "self"); // Verify HATEOAS was added

            // Verify Mediator was called with correct data
            _mockMediator.Verify(x => x.Send(It.Is<LoginCommand>(c =>
                c.Username == "testuser" &&
                c.DeviceName == "PC"), default), Times.Once);
        }

        [Fact]
        public async Task Login_Should_Throw_If_Mediator_Throws_Unauthorized()
        {
            // Arrange
            var request = new LoginRequest { Username = "baduser", Password = "wrong", DeviceName = "PC" };

            // Simulate the Handler throwing an exception (e.g., Wrong Password)
            _mockMediator.Setup(x => x.Send(It.IsAny<LoginCommand>(), default))
                .ThrowsAsync(new UnauthorizedAccessException("Invalid credentials"));

            // Act & Assert
            // The Controller just propagates the exception to the Middleware
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _controller.Login(request));
        }

        // ==========================================
        // LOGOUT TESTS (Fixed for ICurrentUserService)
        // ==========================================

        [Fact]
        public async Task Logout_Should_ReturnOk_When_Mediator_Returns_True()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var userId = Guid.NewGuid();

            // FIX: Mock the Service directly. Do NOT set HttpContext.
            // The Controller asks _currentUserService for these values.
            _mockUserService.Setup(x => x.UserId).Returns(userId);
            _mockUserService.Setup(x => x.SessionId).Returns(sessionId);

            // Simulate Mediator Success
            _mockMediator.Setup(x => x.Send(It.IsAny<LogoutCommand>(), default))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.Logout();

            // Assert
            Assert.IsType<OkObjectResult>(result);

            // FIX: Verify matches the Mocked values above
            _mockMediator.Verify(x => x.Send(It.Is<LogoutCommand>(c =>
                c.UserId == userId &&
                c.SessionId == sessionId), default), Times.Once);
        }

        [Fact]
        public async Task Logout_Should_ReturnNotFound_When_Mediator_Returns_False()
        {
            // Arrange
            _mockUserService.Setup(x => x.UserId).Returns(Guid.NewGuid());
            _mockUserService.Setup(x => x.SessionId).Returns("session-1");

            // Simulate Failure
            _mockMediator.Setup(x => x.Send(It.IsAny<LogoutCommand>(), default))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.Logout();

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task Logout_Should_ThrowArgumentException_WhenTokenIsMalformed()
        {
            // Arrange
            // FIX: The Controller logic relies on ICurrentUserService to throw if the token is bad.
            // So we must Mock that exception here.

            _mockUserService.Setup(x => x.UserId)
                .Throws(new ArgumentException("Invalid Token: User ID is missing."));

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _controller.Logout());

            // Verify Mediator was NEVER called because exception happened first
            _mockMediator.Verify(x => x.Send(It.IsAny<LogoutCommand>(), default), Times.Never);
        }
    }
}