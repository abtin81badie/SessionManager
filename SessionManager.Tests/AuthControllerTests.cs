using Microsoft.AspNetCore.Http; // Required for DefaultHttpContext
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
    public class AuthControllerTests
    {
        private readonly Mock<ISessionRepository> _mockSessionRepo;
        private readonly Mock<IUserRepository> _mockUserRepo;
        private readonly Mock<ICryptoService> _mockCrypto;
        private readonly Mock<ITokenService> _mockToken;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            _mockSessionRepo = new Mock<ISessionRepository>();
            _mockUserRepo = new Mock<IUserRepository>();
            _mockCrypto = new Mock<ICryptoService>();
            _mockToken = new Mock<ITokenService>();

            _controller = new AuthController(
                _mockSessionRepo.Object,
                _mockUserRepo.Object,
                _mockCrypto.Object,
                _mockToken.Object
            );
        }

        // --- HELPER: Generate Fake JWT ---
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
        // LOGIN TESTS (Unchanged mostly, logic is body-based)
        // ==========================================

        [Fact]
        public async Task Login_Should_AutoRegister_IfUserDoesNotExist()
        {
            // Arrange
            var request = new LoginRequest { Username = "newuser", Password = "Password123", DeviceName = "PC" };

            _mockUserRepo.Setup(x => x.GetByUsernameAsync(request.Username))
                .ReturnsAsync((User?)null);

            _mockCrypto.Setup(x => x.Encrypt(It.IsAny<string>()))
                .Returns(("encrypted", "iv"));

            _mockToken.Setup(x => x.GenerateJwt(It.IsAny<User>(), It.IsAny<string>()))
                .Returns("fake-jwt-token");

            // Act
            var result = await _controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            _mockUserRepo.Verify(x => x.CreateUserAsync(It.Is<User>(u => u.Username == "newuser")), Times.Once);
            _mockSessionRepo.Verify(x => x.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<SessionInfo>(), It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public async Task Login_Should_Fail_IfPasswordIsWrong()
        {
            // Arrange
            var request = new LoginRequest { Username = "existing", Password = "WrongPassword", DeviceName = "PC" };
            var existingUser = new User { Username = "existing", PasswordCipherText = "cipher", PasswordIV = "iv" };

            _mockUserRepo.Setup(x => x.GetByUsernameAsync("existing")).ReturnsAsync(existingUser);
            _mockCrypto.Setup(x => x.Decrypt("cipher", "iv")).Returns("RealPassword");

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _controller.Login(request));
        }

        [Fact]
        public async Task Login_Should_Succeed_IfPasswordIsCorrect()
        {
            // Arrange
            var request = new LoginRequest { Username = "existing", Password = "RealPassword", DeviceName = "PC" };
            var existingUser = new User { Username = "existing", PasswordCipherText = "cipher", PasswordIV = "iv" };

            _mockUserRepo.Setup(x => x.GetByUsernameAsync("existing")).ReturnsAsync(existingUser);
            _mockCrypto.Setup(x => x.Decrypt("cipher", "iv")).Returns("RealPassword");
            _mockToken.Setup(x => x.GenerateJwt(It.IsAny<User>(), It.IsAny<string>())).Returns("valid-jwt");

            // Act
            var result = await _controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<LoginResponse>(okResult.Value);
            Assert.Equal("valid-jwt", response.Token);
        }

        // ==========================================
        // LOGOUT TESTS (Updated for Headers)
        // ==========================================

        [Fact]
        public async Task Logout_Should_ReturnOk_WhenTokenIsValidAndSessionDeleted()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var userId = Guid.NewGuid();
            var validToken = GenerateTestJwt(jti: sessionId, sub: userId.ToString());

            // Inject Header
            SetupHttpContext(validToken);

            _mockSessionRepo.Setup(x => x.DeleteSessionAsync(sessionId, userId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.Logout(); // No arguments

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            _mockSessionRepo.Verify(x => x.DeleteSessionAsync(sessionId, userId), Times.Once);
        }

        [Fact]
        public async Task Logout_Should_ReturnNotFound_WhenSessionAlreadyDeleted()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var userId = Guid.NewGuid();
            var validToken = GenerateTestJwt(jti: sessionId, sub: userId.ToString());

            SetupHttpContext(validToken);

            _mockSessionRepo.Setup(x => x.DeleteSessionAsync(sessionId, userId))
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
            // "BadToken" causes JwtSecurityTokenHandler to throw, or SessionValidator validation logic to fail
            SetupHttpContext("BadToken");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _controller.Logout());
        }
    }
}