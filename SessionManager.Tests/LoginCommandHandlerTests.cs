using Moq;
using SessionManager.Application.DTOs;
using SessionManager.Application.Features.Auth.Login;
using SessionManager.Application.Interfaces;
using SessionManager.Application.Models;
using SessionManager.Domain.Entities;
using Xunit;

namespace SessionManager.Tests.Features.Auth.Login
{
    public class LoginCommandHandlerTests
    {
        private readonly Mock<IUserRepository> _mockUserRepo;
        private readonly Mock<ISessionRepository> _mockSessionRepo;
        private readonly Mock<ICryptoService> _mockCrypto;
        private readonly Mock<ITokenService> _mockTokenService;
        private readonly LoginCommandHandler _handler;

        public LoginCommandHandlerTests()
        {
            _mockUserRepo = new Mock<IUserRepository>();
            _mockSessionRepo = new Mock<ISessionRepository>();
            _mockCrypto = new Mock<ICryptoService>();
            _mockTokenService = new Mock<ITokenService>();

            _handler = new LoginCommandHandler(
                _mockUserRepo.Object,
                _mockSessionRepo.Object,
                _mockCrypto.Object,
                _mockTokenService.Object
            );
        }

        [Fact]
        public async Task Handle_Should_Register_And_Login_When_User_Does_Not_Exist()
        {
            // Arrange
            var command = new LoginCommand { Username = "newuser", Password = "password123", DeviceName = "PC" };

            // 1. Mock User Repo returning null (User not found)
            _mockUserRepo.Setup(r => r.GetByUsernameAsync(command.Username))
                .ReturnsAsync((User?)null);

            // 2. Mock Encryption
            _mockCrypto.Setup(c => c.Encrypt(command.Password))
                .Returns(("encrypted_pass", "iv_vector"));

            // 3. Mock Token Generation
            _mockTokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh-token-123");
            _mockTokenService.Setup(t => t.GenerateJwt(It.IsAny<TokenUserDto>(), It.IsAny<string>()))
                .Returns("jwt-token-abc");

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("newuser", result.User.Username);
            Assert.Equal("jwt-token-abc", result.Token);
            Assert.Equal("refresh-token-123", result.RefreshToken);

            // Verify User was Created
            _mockUserRepo.Verify(r => r.CreateUserAsync(It.Is<User>(u =>
                u.Username == command.Username &&
                u.PasswordCipherText == "encrypted_pass")), Times.Once);

            // Verify Session was Created
            _mockSessionRepo.Verify(r => r.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<SessionInfo>()), Times.Once);
        }

        [Fact]
        public async Task Handle_Should_Login_Success_When_User_Exists_And_Password_Correct()
        {
            // Arrange
            var command = new LoginCommand { Username = "existing", Password = "password123", DeviceName = "PC" };
            var existingUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "existing",
                PasswordCipherText = "cipher",
                PasswordIV = "iv",
                Role = "User"
            };

            // 1. Mock User Found
            _mockUserRepo.Setup(r => r.GetByUsernameAsync(command.Username))
                .ReturnsAsync(existingUser);

            // 2. Mock Decryption returning the correct password
            _mockCrypto.Setup(c => c.Decrypt(existingUser.PasswordCipherText, existingUser.PasswordIV))
                .Returns("password123"); // Matches command.Password

            // 3. Mock Tokens
            _mockTokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh-token");
            _mockTokenService.Setup(t => t.GenerateJwt(It.IsAny<TokenUserDto>(), It.IsAny<string>()))
                .Returns("jwt-token");

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal("existing", result.User.Username);
            Assert.Equal("jwt-token", result.Token);

            // Verify we did NOT try to create a new user
            _mockUserRepo.Verify(r => r.CreateUserAsync(It.IsAny<User>()), Times.Never);

            // Verify Session Created
            _mockSessionRepo.Verify(r => r.CreateSessionAsync(existingUser.Id, It.Is<SessionInfo>(s =>
                s.DeviceInfo == "PC")), Times.Once);
        }

        [Fact]
        public async Task Handle_Should_Throw_Unauthorized_When_Password_Incorrect()
        {
            // Arrange
            var command = new LoginCommand { Username = "existing", Password = "wrong_password", DeviceName = "PC" };
            var existingUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "existing",
                PasswordCipherText = "cipher",
                PasswordIV = "iv"
            };

            // 1. Mock User Found
            _mockUserRepo.Setup(r => r.GetByUsernameAsync(command.Username))
                .ReturnsAsync(existingUser);

            // 2. Mock Decryption returning the REAL password (which doesn't match command)
            _mockCrypto.Setup(c => c.Decrypt(existingUser.PasswordCipherText, existingUser.PasswordIV))
                .Returns("correct_password");

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _handler.Handle(command, CancellationToken.None));

            Assert.Equal("Invalid username or password.", ex.Message);

            // Verify Session was NEVER Created
            _mockSessionRepo.Verify(r => r.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<SessionInfo>()), Times.Never);
        }
    }
}