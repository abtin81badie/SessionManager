using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using SessionManager.Application.DTOs;
using SessionManager.Application.Features.Auth.RefreshToken;
using SessionManager.Application.Interfaces;
using SessionManager.Application.Models;
using SessionManager.Domain.Entities;
using SessionManager.Infrastructure.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Xunit;

namespace SessionManager.Tests.Features.Auth.RefreshToken
{
    public class RefreshTokenCommandHandlerTests
    {
        private readonly Mock<ISessionRepository> _mockSessionRepo;
        private readonly Mock<ITokenService> _mockTokenService;
        private readonly Mock<IUserRepository> _mockUserRepo;
        private readonly Mock<IOptions<RefreshTokenOptions>> _mockOptions;
        private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
        private readonly RefreshTokenCommandHandler _handler;

        public RefreshTokenCommandHandlerTests()
        {
            _mockSessionRepo = new Mock<ISessionRepository>();
            _mockTokenService = new Mock<ITokenService>();
            _mockUserRepo = new Mock<IUserRepository>();
            _mockOptions = new Mock<IOptions<RefreshTokenOptions>>();
            _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

            // Setup Default Options
            _mockOptions.Setup(o => o.Value).Returns(new RefreshTokenOptions { ExpiryMinutes = 60 });

            _handler = new RefreshTokenCommandHandler(
                _mockSessionRepo.Object,
                _mockTokenService.Object,
                _mockUserRepo.Object,
                _mockOptions.Object,
                _mockHttpContextAccessor.Object
            );
        }

        // --- HELPER: Setup HttpContext Headers ---
        private void SetupHeaders(string accessToken, string refreshToken)
        {
            var context = new DefaultHttpContext();
            if (!string.IsNullOrEmpty(accessToken))
                context.Request.Headers["Authorization"] = $"Bearer {accessToken}";

            if (!string.IsNullOrEmpty(refreshToken))
                context.Request.Headers["X-Refresh-Token"] = refreshToken;

            _mockHttpContextAccessor.Setup(h => h.HttpContext).Returns(context);
        }

        // --- HELPER: Generate Expired JWT with JTI ---
        private string GenerateTestJwt(string jti)
        {
            var claims = new List<Claim> { new Claim(JwtRegisteredClaimNames.Jti, jti) };
            var token = new JwtSecurityToken(claims: claims);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [Fact]
        public async Task Handle_Should_Rotate_Tokens_On_Success()
        {
            // Arrange
            var oldSessionId = Guid.NewGuid().ToString();
            var expiredJwt = GenerateTestJwt(oldSessionId);
            var clientRefreshToken = "opaque-refresh-token";
            var userId = Guid.NewGuid();

            // 1. Setup Headers
            SetupHeaders(expiredJwt, clientRefreshToken);

            // 2. Mock Session Found in Redis
            var sessionInfo = new SessionInfo
            {
                Token = oldSessionId,
                UserId = userId,
                LastActiveAt = DateTime.UtcNow // Recently active
            };
            _mockSessionRepo.Setup(r => r.GetSessionAsync(oldSessionId))
                .ReturnsAsync(sessionInfo);

            // 3. Mock User Exists
            var user = new User { Id = userId, Username = "test", Role = "User" };
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);

            // 4. Mock New Token Generation
            _mockTokenService.Setup(t => t.GenerateRefreshToken()).Returns("new-refresh-token");
            _mockTokenService.Setup(t => t.GenerateJwt(It.IsAny<TokenUserDto>(), It.IsAny<string>()))
                .Returns("new-access-token");

            // Act
            var result = await _handler.Handle(new RefreshTokenCommand(), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("new-access-token", result.AccessToken);
            Assert.Equal("new-refresh-token", result.RefreshToken);

            // Verify Rotation: Old session deleted, New session created
            _mockSessionRepo.Verify(r => r.DeleteSessionAsync(oldSessionId, userId), Times.Once);
            _mockSessionRepo.Verify(r => r.CreateSessionAsync(userId, It.IsAny<SessionInfo>()), Times.Once);
        }

        [Fact]
        public async Task Handle_Should_Throw_Unauthorized_When_AuthorizationHeader_Missing()
        {
            // Arrange
            SetupHeaders("", "refresh-token"); // Empty Access Token

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _handler.Handle(new RefreshTokenCommand(), CancellationToken.None));

            Assert.Equal("Missing or invalid 'Authorization' header.", ex.Message);
        }

        [Fact]
        public async Task Handle_Should_Throw_Unauthorized_When_RefreshTokenHeader_Missing()
        {
            // Arrange
            SetupHeaders("valid-jwt-structure", ""); // Empty Refresh Token

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _handler.Handle(new RefreshTokenCommand(), CancellationToken.None));

            Assert.Equal("Missing 'X-Refresh-Token' header.", ex.Message);
        }

        [Fact]
        public async Task Handle_Should_Throw_Unauthorized_When_Session_NotFound_In_Redis()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var jwt = GenerateTestJwt(sessionId);
            SetupHeaders(jwt, "opaque-ref");

            // Mock Session NOT found (null)
            _mockSessionRepo.Setup(r => r.GetSessionAsync(sessionId))
                .ReturnsAsync((SessionInfo?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _handler.Handle(new RefreshTokenCommand(), CancellationToken.None));

            Assert.Contains("Session not found", ex.Message);
        }

        [Fact]
        public async Task Handle_Should_Throw_Unauthorized_And_Delete_Session_When_Expired()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var userId = Guid.NewGuid();
            var jwt = GenerateTestJwt(sessionId);
            SetupHeaders(jwt, "opaque-ref");

            // Mock Session Found BUT Expired
            // Expired 61 minutes ago (Limit is 60)
            var sessionInfo = new SessionInfo
            {
                Token = sessionId,
                UserId = userId,
                LastActiveAt = DateTime.UtcNow.AddMinutes(-61)
            };

            _mockSessionRepo.Setup(r => r.GetSessionAsync(sessionId)).ReturnsAsync(sessionInfo);
            _mockOptions.Setup(o => o.Value).Returns(new RefreshTokenOptions { ExpiryMinutes = 60 });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _handler.Handle(new RefreshTokenCommand(), CancellationToken.None));

            Assert.Equal("Refresh token expired.", ex.Message);

            // Critical Security Check: Ensure the expired session was deleted from DB
            _mockSessionRepo.Verify(r => r.DeleteSessionAsync(sessionId, userId), Times.Once);
        }

        [Fact]
        public async Task Handle_Should_Throw_Unauthorized_When_AccessToken_Is_Malformed()
        {
            // Arrange
            // Passing a random string that isn't a JWT, so JTI extraction fails
            SetupHeaders("malformed-jwt-string", "opaque-ref");

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _handler.Handle(new RefreshTokenCommand(), CancellationToken.None));

            Assert.Equal("Could not extract Session ID (jti) from expired token.", ex.Message);
        }
    }
}