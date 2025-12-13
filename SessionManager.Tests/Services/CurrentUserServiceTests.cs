using Microsoft.AspNetCore.Http;
using Moq;
using SessionManager.Infrastructure.Services;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt; // For JwtRegisteredClaimNames
using Xunit;

namespace SessionManager.Tests.Services
{
    public class CurrentUserServiceTests
    {
        private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
        private readonly CurrentUserService _service;

        public CurrentUserServiceTests()
        {
            _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            _service = new CurrentUserService(_mockHttpContextAccessor.Object);
        }

        // --- HELPER: Setup HttpContext with specific Claims ---
        private void SetupUser(bool isAuthenticated, IEnumerable<Claim>? claims = null)
        {
            var context = new DefaultHttpContext();

            if (isAuthenticated)
            {
                var identity = new ClaimsIdentity(claims, "TestAuthType"); // AuthType is required for IsAuthenticated = true
                context.User = new ClaimsPrincipal(identity);
            }
            else
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity()); // Unauthenticated
            }

            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);
        }

        // ==========================================
        // 1. IsAuthenticated Tests
        // ==========================================

        [Fact]
        public void IsAuthenticated_Should_Return_True_When_User_Is_Logged_In()
        {
            // Arrange
            SetupUser(isAuthenticated: true);

            // Act & Assert
            Assert.True(_service.IsAuthenticated);
        }

        [Fact]
        public void IsAuthenticated_Should_Return_False_When_User_Is_Anonymous()
        {
            // Arrange
            SetupUser(isAuthenticated: false);

            // Act & Assert
            Assert.False(_service.IsAuthenticated);
        }

        [Fact]
        public void IsAuthenticated_Should_Return_False_When_HttpContext_Is_Null()
        {
            // Arrange
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

            // Act & Assert
            Assert.False(_service.IsAuthenticated);
        }

        // ==========================================
        // 2. UserId Tests
        // ==========================================

        [Fact]
        public void UserId_Should_Return_Guid_From_NameIdentifier_Claim()
        {
            // Arrange
            var expectedId = Guid.NewGuid();
            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, expectedId.ToString()) };
            SetupUser(true, claims);

            // Act
            var result = _service.UserId;

            // Assert
            Assert.Equal(expectedId, result);
        }

        [Fact]
        public void UserId_Should_Return_Guid_From_Sub_Claim_If_NameIdentifier_Missing()
        {
            // Arrange
            var expectedId = Guid.NewGuid();
            var claims = new List<Claim> { new Claim(JwtRegisteredClaimNames.Sub, expectedId.ToString()) };
            SetupUser(true, claims);

            // Act
            var result = _service.UserId;

            // Assert
            Assert.Equal(expectedId, result);
        }

        [Fact]
        public void UserId_Should_Throw_Unauthorized_If_Claim_Missing()
        {
            // Arrange
            SetupUser(true, new List<Claim>()); // No ID claims

            // Act & Assert
            var ex = Assert.Throws<UnauthorizedAccessException>(() => _service.UserId);
            Assert.Equal("User ID claim is missing or invalid.", ex.Message);
        }

        [Fact]
        public void UserId_Should_Throw_Unauthorized_If_Guid_Format_Is_Invalid()
        {
            // Arrange
            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, "invalid-guid-string") };
            SetupUser(true, claims);

            // Act & Assert
            var ex = Assert.Throws<UnauthorizedAccessException>(() => _service.UserId);
            Assert.Equal("User ID claim is missing or invalid.", ex.Message);
        }

        // ==========================================
        // 3. SessionId Tests
        // ==========================================

        [Fact]
        public void SessionId_Should_Return_Value_From_Jti_Claim()
        {
            // Arrange
            var expectedSessionId = "session-123-abc";
            var claims = new List<Claim> { new Claim(JwtRegisteredClaimNames.Jti, expectedSessionId) };
            SetupUser(true, claims);

            // Act
            var result = _service.SessionId;

            // Assert
            Assert.Equal(expectedSessionId, result);
        }

        [Fact]
        public void SessionId_Should_Throw_Unauthorized_If_Jti_Missing()
        {
            // Arrange
            SetupUser(true, new List<Claim>()); // No JTI

            // Act & Assert
            var ex = Assert.Throws<UnauthorizedAccessException>(() => _service.SessionId);
            Assert.Equal("Session ID (jti) claim is missing.", ex.Message);
        }

        // ==========================================
        // 4. Role Tests
        // ==========================================

        [Fact]
        public void Role_Should_Return_Role_From_Claim()
        {
            // Arrange
            var claims = new List<Claim> { new Claim(ClaimTypes.Role, "Admin") };
            SetupUser(true, claims);

            // Act
            var result = _service.Role;

            // Assert
            Assert.Equal("Admin", result);
        }

        [Fact]
        public void Role_Should_Return_Default_User_If_Claim_Missing()
        {
            // Arrange
            SetupUser(true, new List<Claim>()); // No Role claim

            // Act
            var result = _service.Role;

            // Assert
            Assert.Equal("User", result); // Checks the "?? 'User'" fallback
        }
    }
}