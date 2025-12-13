using Microsoft.Extensions.Options;
using Moq;
using SessionManager.Application.DTOs;
using SessionManager.Application.Models;
using SessionManager.Infrastructure.Options;
using SessionManager.Infrastructure.Services;
using System.IdentityModel.Tokens.Jwt; // Needed to decode the token in tests
using Xunit;

namespace SessionManager.Tests.Services
{
    public class JwtServiceTests
    {
        private readonly Mock<IOptions<JwtOptions>> _mockOptions;
        private readonly JwtService _service;

        // Shared test data
        private readonly string _secretKey = "super_secret_key_that_is_long_enough_for_hmac_sha256";
        private readonly string _issuer = "TestIssuer";
        private readonly string _audience = "TestAudience";

        public JwtServiceTests()
        {
            _mockOptions = new Mock<IOptions<JwtOptions>>();

            // Setup default valid options
            _mockOptions.Setup(o => o.Value).Returns(new JwtOptions
            {
                Secret = _secretKey,
                Issuer = _issuer,
                Audience = _audience,
                ExpiryMinutes = 60
            });

            _service = new JwtService(_mockOptions.Object);
        }

        // ==========================================
        // 1. GenerateJwt Tests
        // ==========================================

        [Fact]
        public void GenerateJwt_Should_Return_Valid_Token_String()
        {
            // Arrange
            var user = new TokenUserDto { Id = Guid.NewGuid(), Username = "testuser", Role = "Admin" };
            var sessionId = Guid.NewGuid().ToString();

            // Act
            var token = _service.GenerateJwt(user, sessionId);

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(token));
            Assert.Equal(3, token.Split('.').Length); // Standard JWT has 3 parts (Header.Payload.Signature)
        }

        [Fact]
        public void GenerateJwt_Should_Include_Correct_Claims()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var sessionId = Guid.NewGuid().ToString();
            var user = new TokenUserDto { Id = userId, Username = "jane_doe", Role = "User" };

            // Act
            var tokenString = _service.GenerateJwt(user, sessionId);

            // Assert (Decode the token to verify contents)
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(tokenString);

            // 1. Verify Standard Claims
            Assert.Equal(_issuer, token.Issuer);
            Assert.Equal(_audience, token.Audiences.First());

            // 2. Verify Custom Claims
            var subClaim = token.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
            var jtiClaim = token.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
            var roleClaim = token.Claims.First(c => c.Type == "role").Value; // ClaimTypes.Role maps to "role" usually
            var usernameClaim = token.Claims.First(c => c.Type == "username").Value;

            Assert.Equal(userId.ToString(), subClaim);
            Assert.Equal(sessionId, jtiClaim);
            Assert.Equal("User", roleClaim);
            Assert.Equal("jane_doe", usernameClaim);
        }

        [Fact]
        public void GenerateJwt_Should_Set_Correct_Expiration()
        {
            // Arrange
            _mockOptions.Setup(o => o.Value).Returns(new JwtOptions
            {
                Secret = _secretKey,
                ExpiryMinutes = 15 // Short expiry
            });
            var service = new JwtService(_mockOptions.Object);

            var user = new TokenUserDto { Id = Guid.NewGuid(), Username = "test", Role = "User" };

            // Act
            var tokenString = service.GenerateJwt(user, "session-1");
            var token = new JwtSecurityTokenHandler().ReadJwtToken(tokenString);

            // Assert
            // Allow small delta for execution time (e.g., 5 seconds)
            var expectedExpiry = DateTime.UtcNow.AddMinutes(15);
            var actualExpiry = token.ValidTo;

            // Note: ValidTo is in UTC.
            Assert.InRange(actualExpiry, expectedExpiry.AddSeconds(-10), expectedExpiry.AddSeconds(10));
        }

        // ==========================================
        // 2. GenerateRefreshToken Tests
        // ==========================================

        [Fact]
        public void GenerateRefreshToken_Should_Return_Valid_Base64_String()
        {
            // Act
            var token = _service.GenerateRefreshToken();

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(token));

            // Try to decode Base64 to verify format
            var bytes = Convert.FromBase64String(token);
            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void GenerateRefreshToken_Should_Return_Correct_Length()
        {
            // Act
            var token = _service.GenerateRefreshToken();

            // Assert
            // 32 bytes input -> Base64 output length formula: Ceiling(32 / 3) * 4 = 44 characters
            Assert.Equal(44, token.Length);

            // Verify byte length
            var bytes = Convert.FromBase64String(token);
            Assert.Equal(32, bytes.Length);
        }

        [Fact]
        public void GenerateRefreshToken_Should_Be_Unique()
        {
            // Act
            var token1 = _service.GenerateRefreshToken();
            var token2 = _service.GenerateRefreshToken();

            // Assert
            Assert.NotEqual(token1, token2);
        }
    }
}