using Moq;
using SessionManager.Application.Features.Auth.Logout;
using SessionManager.Application.Interfaces;
using Xunit;

namespace SessionManager.Tests.Features.Auth.Logout
{
    public class LogoutCommandHandlerTests
    {
        private readonly Mock<ISessionRepository> _mockRepo;
        private readonly LogoutCommandHandler _handler;

        public LogoutCommandHandlerTests()
        {
            _mockRepo = new Mock<ISessionRepository>();
            _handler = new LogoutCommandHandler(_mockRepo.Object);
        }

        [Fact]
        public async Task Handle_Should_ReturnTrue_When_SessionIsDeletedSuccessfully()
        {
            // Arrange
            var command = new LogoutCommand
            {
                UserId = Guid.NewGuid(),
                SessionId = Guid.NewGuid().ToString()
            };

            // Setup repository to return true (success)
            _mockRepo.Setup(r => r.DeleteSessionAsync(command.SessionId, command.UserId))
                .ReturnsAsync(true);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.True(result);

            // Verify repository was called exactly once with correct params
            _mockRepo.Verify(r => r.DeleteSessionAsync(command.SessionId, command.UserId), Times.Once);
        }

        [Fact]
        public async Task Handle_Should_ReturnFalse_When_SessionNotFound_Or_DeleteFails()
        {
            // Arrange
            var command = new LogoutCommand
            {
                UserId = Guid.NewGuid(),
                SessionId = Guid.NewGuid().ToString()
            };

            // Setup repository to return false (failure/not found)
            _mockRepo.Setup(r => r.DeleteSessionAsync(command.SessionId, command.UserId))
                .ReturnsAsync(false);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result);
        }
    }
}