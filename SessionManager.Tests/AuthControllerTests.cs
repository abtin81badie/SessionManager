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

        // --- HELPER: Generate Fake JWT ---
        private string GenerateTestJwt(string? jti = null, string? sub = null)
        {
            var claims = new List<Claim> { new Claim("role", "User") };

            if (jti != null) claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));
            if (sub != null) claims.Add(new Claim(JwtRegisteredClaimNames.Sub, sub));

            var token = new JwtSecurityToken(claims: claims);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public AuthControllerTests()
        {
            // 1. Create Mocks
            _mockSessionRepo = new Mock<ISessionRepository>();
            _mockUserRepo = new Mock<IUserRepository>();
            _mockCrypto = new Mock<ICryptoService>();
            _mockToken = new Mock<ITokenService>();

            // 2. Initialize Controller with Mocks
            _controller = new AuthController(
                _mockSessionRepo.Object,
                _mockUserRepo.Object,
                _mockCrypto.Object,
                _mockToken.Object
            );
        }

        [Fact]
        public async Task Login_Should_AutoRegister_IfUserDoesNotExist()
        {
            // Arrange
            var request = new LoginRequest { Username = "newuser", Password = "Password123", DeviceName = "PC" };

            // Setup: User Repo returns NULL (User doesn't exist)
            _mockUserRepo.Setup(x => x.GetByUsernameAsync(request.Username))
                .ReturnsAsync((User?)null);

            // Setup: Crypto service mocks
            _mockCrypto.Setup(x => x.Encrypt(It.IsAny<string>()))
                .Returns(("encrypted", "iv"));

            // Setup: Token service
            _mockToken.Setup(x => x.GenerateJwt(It.IsAny<User>(), It.IsAny<string>()))
                .Returns("fake-jwt-token");

            // Act
            var result = await _controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);

            // Verify CreateUserAsync was called (Auto Registration)
            _mockUserRepo.Verify(x => x.CreateUserAsync(It.Is<User>(u => u.Username == "newuser")), Times.Once);

            // Verify Session was created in Redis
            _mockSessionRepo.Verify(x => x.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<SessionInfo>(), It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public async Task Login_Should_Fail_IfPasswordIsWrong()
        {
            // Arrange
            var request = new LoginRequest { Username = "existing", Password = "WrongPassword", DeviceName = "PC" };
            var existingUser = new User { Username = "existing", PasswordCipherText = "cipher", PasswordIV = "iv" };

            // Setup: User Exists
            _mockUserRepo.Setup(x => x.GetByUsernameAsync("existing"))
                .ReturnsAsync(existingUser);

            // Setup: Decrypt returns the "Real" password
            _mockCrypto.Setup(x => x.Decrypt("cipher", "iv"))
                .Returns("RealPassword");

            // Act & Assert
            // Since we throw Exception in Controller, expected behavior is an Exception 
            // (Which ExceptionMiddleware handles in real app, but here we catch the exception directly)
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _controller.Login(request));

            // Verify we NEVER generated a token or session
            _mockSessionRepo.Verify(x => x.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<SessionInfo>(), It.IsAny<TimeSpan>()), Times.Never);
        }

        [Fact]
        public async Task Login_Should_Succeed_IfPasswordIsCorrect()
        {
            // Arrange
            var request = new LoginRequest { Username = "existing", Password = "RealPassword", DeviceName = "PC" };
            var existingUser = new User { Username = "existing", PasswordCipherText = "cipher", PasswordIV = "iv" };

            _mockUserRepo.Setup(x => x.GetByUsernameAsync("existing"))
                .ReturnsAsync(existingUser);

            // Setup: Decrypt returns matching password
            _mockCrypto.Setup(x => x.Decrypt("cipher", "iv"))
                .Returns("RealPassword");

            _mockToken.Setup(x => x.GenerateJwt(It.IsAny<User>(), It.IsAny<string>()))
                .Returns("valid-jwt");

            // Act
            var result = await _controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<LoginResponse>(okResult.Value);

            Assert.Equal("valid-jwt", response.Token);
        }

        [Fact]
        public async Task Logout_Should_ReturnOk_WhenTokenIsValidAndSessionDeleted()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var userId = Guid.NewGuid();
            var validToken = GenerateTestJwt(jti: sessionId, sub: userId.ToString());

            var request = new LogoutRequest { Token = validToken };

            // Setup: Repository returns TRUE (Deletion successful)
            _mockSessionRepo.Setup(x => x.DeleteSessionAsync(sessionId, userId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.Logout(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            // Verify we called Delete with correct extracted IDs
            _mockSessionRepo.Verify(x => x.DeleteSessionAsync(sessionId, userId), Times.Once);
        }

        [Fact]
        public async Task Logout_Should_ReturnNotFound_WhenSessionAlreadyDeleted()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var userId = Guid.NewGuid();
            var validToken = GenerateTestJwt(jti: sessionId, sub: userId.ToString());

            var request = new LogoutRequest { Token = validToken };

            // Setup: Repository returns FALSE (Key didn't exist)
            _mockSessionRepo.Setup(x => x.DeleteSessionAsync(sessionId, userId))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.Logout(request);

            // Assert
            var notFound = Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task Logout_Should_ReturnBadRequest_WhenTokenIsMalformed()
        {
            // Arrange
            // "BadToken" has no dots, so SessionValidator will throw ArgumentException
            var request = new LogoutRequest { Token = "BadToken" };

            // Act & Assert
            // In Unit Tests, middleware doesn't run, so we expect the Exception directly
            await Assert.ThrowsAsync<ArgumentException>(() => _controller.Logout(request));
        }

        [Fact]
        public async Task Logout_Should_ReturnBadRequest_WhenClaimsAreMissing()
        {
            // Arrange
            // Token has structure but NO jti or sub claims
            var tokenWithoutClaims = GenerateTestJwt(jti: null, sub: null);
            var request = new LogoutRequest { Token = tokenWithoutClaims };

            // Act
            var result = await _controller.Logout(request);

            // Assert
            // The controller logic checks for claims and returns BadRequest if null
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}