using Moq;
using SessionManager.Application.Features.Sessions.Renew;
using SessionManager.Application.Interfaces;
using SessionManager.Domain.Entities;
using Xunit;

namespace SessionManager.Tests.Features.Sessions.Renew
{
    public class RenewSessionCommandHandlerTests
    {
        private readonly Mock<ISessionRepository> _mockRepo;
        private readonly RenewSessionCommandHandler _handler;

        public RenewSessionCommandHandlerTests()
        {
            _mockRepo = new Mock<ISessionRepository>();
            _handler = new RenewSessionCommandHandler(_mockRepo.Object);
        }

        [Fact]
        public async Task Handle_Should_ReturnTrue_And_Extend_When_SessionExists()
        {
            // Arrange
            var command = new RenewSessionCommand
            {
                UserId = Guid.NewGuid(),
                SessionId = "valid-session-id"
            };

            // Mock: Session exists in DB
            _mockRepo.Setup(r => r.GetSessionAsync(command.SessionId))
                .ReturnsAsync(new SessionInfo { Token = command.SessionId });

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.True(result);

            // Verify: We checked existence first
            _mockRepo.Verify(r => r.GetSessionAsync(command.SessionId), Times.Once);

            // Verify: We actually called the Extend method
            _mockRepo.Verify(r => r.ExtendSessionAsync(command.UserId, command.SessionId), Times.Once);
        }

        [Fact]
        public async Task Handle_Should_ReturnFalse_When_Session_NotFound()
        {
            // Arrange
            var command = new RenewSessionCommand
            {
                UserId = Guid.NewGuid(),
                SessionId = "expired-or-invalid-id"
            };

            // Mock: Session returns null (Not Found)
            _mockRepo.Setup(r => r.GetSessionAsync(command.SessionId))
                .ReturnsAsync((SessionInfo?)null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result);

            // Verify: We checked existence
            _mockRepo.Verify(r => r.GetSessionAsync(command.SessionId), Times.Once);

            // Verify: We NEVER tried to extend because it didn't exist
            _mockRepo.Verify(r => r.ExtendSessionAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
        }
    }
}