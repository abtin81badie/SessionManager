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

        // Helper to generate a dummy JWT for testing
        private string GenerateTestJwt(string? jti = null)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, "user-123"),
                new Claim("role", "User")
            };

            if (jti != null)
            {
                claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));
            }

            // Create a token without signing for unit testing (since we just parse claims)
            // Note: In real integration tests, you'd need a valid signature.
            // For unit tests of the logic "Extract JTI", this works if the handler accepts unsigned tokens by default or we bypass validation logic.
            // However, JwtSecurityTokenHandler.ReadJwtToken does NOT validate signature, it just reads.
            var token = new JwtSecurityToken(claims: claims);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [Fact]
        public async Task RenewSession_Should_ReturnOk_WhenTokenIsValidAndSessionExists()
        {
            // Arrange
            var redisSessionId = Guid.NewGuid().ToString();
            var validJwt = GenerateTestJwt(jti: redisSessionId);
            var userId = Guid.NewGuid();

            var request = new RenewSessionRequest { Token = validJwt };

            var existingSession = new SessionInfo
            {
                Token = redisSessionId,
                UserId = userId,
                DeviceInfo = "TestDevice"
            };

            // Setup: Repository finds the session
            _mockSessionRepo.Setup(x => x.GetSessionAsync(redisSessionId))
                .ReturnsAsync(existingSession);

            // Act
            var result = await _controller.RenewSession(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<RenewSessionResponse>(okResult.Value);

            Assert.Equal("Session renewed successfully.", response.Message);

            // Verify ExtendSessionAsync was called with correct ID
            _mockSessionRepo.Verify(x => x.ExtendSessionAsync(userId, redisSessionId, It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public async Task RenewSession_Should_ReturnBadRequest_WhenTokenIsMalformed()
        {
            // Arrange
            // "InvalidToken" has no dots, will fail SessionValidator
            var request = new RenewSessionRequest { Token = "InvalidToken" };

            // Act & Assert
            // The Validator throws ArgumentException, which Middleware catches.
            // But in Unit Tests, middleware doesn't run automatically unless we integration test.
            // So we expect the EXCEPTION here directly.
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _controller.RenewSession(request));
            Assert.Contains("Invalid Token format", ex.Message);
        }

        [Fact]
        public async Task RenewSession_Should_ReturnBadRequest_WhenJtiClaimIsMissing()
        {
            // Arrange
            var jwtWithoutJti = GenerateTestJwt(jti: null);
            var request = new RenewSessionRequest { Token = jwtWithoutJti };

            // Act
            var result = await _controller.RenewSession(request);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            // We need to access the anonymous type or object returned
            // Since we returned `new { Message = ... }`, we can check the value via reflection or string
            var value = badRequest.Value?.ToString();
            Assert.NotNull(value);
        }

        [Fact]
        public async Task RenewSession_Should_ReturnNotFound_WhenSessionDoesNotExistInRedis()
        {
            // Arrange
            var redisSessionId = Guid.NewGuid().ToString();
            var validJwt = GenerateTestJwt(jti: redisSessionId);
            var request = new RenewSessionRequest { Token = validJwt };

            // Setup: Repository returns NULL (Session expired/evicted)
            _mockSessionRepo.Setup(x => x.GetSessionAsync(redisSessionId))
                .ReturnsAsync((SessionInfo?)null);

            // Act
            var result = await _controller.RenewSession(request);

            // Assert
            var notFound = Assert.IsType<NotFoundObjectResult>(result);
        }
    }
}