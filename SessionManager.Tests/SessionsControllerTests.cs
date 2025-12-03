using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SessionManager.Api.Controllers;
using SessionManager.Application.DTOs;
using SessionManager.Application.Interfaces;
using SessionManager.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Xunit;

namespace SessionManager.Tests
{
    public class SessionsControllerTests
    {
        private readonly Mock<ISessionRepository> _mockSessionRepo;
        private readonly SessionsController _controller;

        public SessionsControllerTests()
        {
            _mockSessionRepo = new Mock<ISessionRepository>();
            _controller = new SessionsController(_mockSessionRepo.Object);
        }

        // --- HELPER: Generate Fake JWT ---
        private string GenerateTestJwt(string? jti = null, string? sub = null)
        {
            var claims = new List<Claim>
            {
                new Claim("role", "User")
            };

            if (sub != null) claims.Add(new Claim(JwtRegisteredClaimNames.Sub, sub));
            else claims.Add(new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()));

            if (jti != null) claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));

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
        // RENEW TESTS
        // ==========================================

        [Fact]
        public async Task RenewSession_Should_ReturnOk_WhenTokenIsValidAndSessionExists()
        {
            // Arrange
            var redisSessionId = Guid.NewGuid().ToString();
            var userId = Guid.NewGuid();
            var validJwt = GenerateTestJwt(jti: redisSessionId, sub: userId.ToString());

            // Mock Header
            SetupHttpContext(validJwt);

            var existingSession = new SessionInfo
            {
                Token = redisSessionId,
                UserId = userId,
                DeviceInfo = "TestDevice"
            };

            _mockSessionRepo.Setup(x => x.GetSessionAsync(redisSessionId))
                .ReturnsAsync(existingSession);

            // Act
            var result = await _controller.RenewSession(); // No args

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<RenewSessionResponse>(okResult.Value);

            Assert.Equal("Session renewed successfully.", response.Message);
            Assert.Equal(validJwt, response.Token); // Should echo the token back

            _mockSessionRepo.Verify(x => x.ExtendSessionAsync(userId, redisSessionId, It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public async Task RenewSession_Should_ThrowArgumentException_WhenTokenIsMalformed()
        {
            // Arrange
            SetupHttpContext("InvalidToken");

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _controller.RenewSession());
            // Assert.Contains("Invalid Token", ex.Message); // Optional check depending on your Validator message
        }

        [Fact]
        public async Task RenewSession_Should_ReturnNotFound_WhenSessionDoesNotExistInRedis()
        {
            // Arrange
            var redisSessionId = Guid.NewGuid().ToString();
            var validJwt = GenerateTestJwt(jti: redisSessionId);

            SetupHttpContext(validJwt);

            _mockSessionRepo.Setup(x => x.GetSessionAsync(redisSessionId))
                .ReturnsAsync((SessionInfo?)null);

            // Act
            var result = await _controller.RenewSession();

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        // ==========================================
        // GET ACTIVE SESSIONS TESTS (New)
        // ==========================================

        [Fact]
        public async Task GetActiveSessions_Should_ReturnList_WhenTokenIsValid()
        {
            // Arrange
            var currentSessionId = Guid.NewGuid().ToString();
            var otherSessionId = Guid.NewGuid().ToString();
            var userId = Guid.NewGuid();

            // Mock Request with Current Session Token
            var validJwt = GenerateTestJwt(jti: currentSessionId, sub: userId.ToString());
            SetupHttpContext(validJwt);

            var sessionsFromRepo = new List<SessionInfo>
            {
                new SessionInfo { Token = currentSessionId, UserId = userId, DeviceInfo = "CurrentDevice", CreatedAt = DateTime.UtcNow },
                new SessionInfo { Token = otherSessionId, UserId = userId, DeviceInfo = "OtherDevice", CreatedAt = DateTime.UtcNow.AddMinutes(-10) }
            };

            _mockSessionRepo.Setup(x => x.GetActiveSessionsAsync(userId))
                .ReturnsAsync(sessionsFromRepo);

            // Act
            var result = await _controller.GetActiveSessions();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var dtos = Assert.IsAssignableFrom<IEnumerable<SessionDto>>(okResult.Value);

            Assert.Equal(2, dtos.Count());

            // Check flags
            var currentDto = dtos.First(x => x.Token == currentSessionId);
            Assert.True(currentDto.IsCurrentSession);

            var otherDto = dtos.First(x => x.Token == otherSessionId);
            Assert.False(otherDto.IsCurrentSession);
        }
    }
}