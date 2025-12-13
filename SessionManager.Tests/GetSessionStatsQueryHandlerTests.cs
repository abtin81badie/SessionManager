using Moq;
using SessionManager.Application.DTOs;
using SessionManager.Application.Features.Admin.GetStats;
using SessionManager.Application.Interfaces;
using SessionManager.Domain.Entities;
using Xunit;

namespace SessionManager.Tests.Features.Admin.GetStats
{
    public class GetSessionStatsQueryHandlerTests
    {
        private readonly Mock<ISessionRepository> _mockRepo;
        private readonly GetSessionStatsQueryHandler _handler;

        public GetSessionStatsQueryHandlerTests()
        {
            _mockRepo = new Mock<ISessionRepository>();
            _handler = new GetSessionStatsQueryHandler(_mockRepo.Object);
        }

        [Fact]
        public async Task Handle_Should_ReturnNull_When_CurrentSession_IsInvalid()
        {
            // Arrange
            var query = new GetSessionStatsQuery
            {
                UserId = Guid.NewGuid(),
                Role = "User",
                CurrentSessionId = "invalid-session-id"
            };

            // Mock: Session Lookup returns null (Session expired/revoked)
            _mockRepo.Setup(r => r.GetSessionAsync(query.CurrentSessionId))
                .ReturnsAsync((SessionInfo?)null);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Null(result);

            // Verify: We should NOT have attempted to fetch stats or extend session
            _mockRepo.Verify(r => r.GetSessionStatsAsync(It.IsAny<Guid?>()), Times.Never);
            _mockRepo.Verify(r => r.ExtendSessionAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Handle_Should_Fetch_All_Stats_If_Role_Is_Admin()
        {
            // Arrange
            var query = new GetSessionStatsQuery
            {
                UserId = Guid.NewGuid(),
                Role = "Admin",
                CurrentSessionId = "admin-session-123"
            };

            // 1. Mock Session Check (Valid)
            _mockRepo.Setup(r => r.GetSessionAsync(query.CurrentSessionId))
                .ReturnsAsync(new SessionInfo());

            // 2. Mock Stats Fetching
            // IMPORTANT: Expecting `null` as the argument because Role is Admin
            var mockStats = new SessionStatsDto
            {
                DetailedSessions = new List<SessionDetailDto>
                {
                    new SessionDetailDto { Token = "other-user-session" },
                    new SessionDetailDto { Token = "admin-session-123" } // The current one
                }
            };

            _mockRepo.Setup(r => r.GetSessionStatsAsync(null)) // Verify NULL was passed
                .ReturnsAsync(mockStats);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);

            // Verify Logic: IsCurrentSession flag set correctly
            var current = result.DetailedSessions.First(s => s.Token == "admin-session-123");
            var other = result.DetailedSessions.First(s => s.Token == "other-user-session");

            Assert.True(current.IsCurrentSession);
            Assert.False(other.IsCurrentSession);

            // Verify: Session was extended
            _mockRepo.Verify(r => r.ExtendSessionAsync(query.UserId, query.CurrentSessionId), Times.Once);
        }

        [Fact]
        public async Task Handle_Should_Fetch_Specific_User_Stats_If_Role_Is_User()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var query = new GetSessionStatsQuery
            {
                UserId = userId,
                Role = "User",
                CurrentSessionId = "user-session-123"
            };

            // 1. Mock Session Check (Valid)
            _mockRepo.Setup(r => r.GetSessionAsync(query.CurrentSessionId))
                .ReturnsAsync(new SessionInfo());

            // 2. Mock Stats Fetching
            // IMPORTANT: Expecting `userId` as the argument because Role is User
            _mockRepo.Setup(r => r.GetSessionStatsAsync(userId))
                .ReturnsAsync(new SessionStatsDto { DetailedSessions = new List<SessionDetailDto>() });

            // Act
            await _handler.Handle(query, CancellationToken.None);

            // Assert
            // Verify: Specifically called with the User's ID, not null
            _mockRepo.Verify(r => r.GetSessionStatsAsync(userId), Times.Once);

            // Verify: Session extended
            _mockRepo.Verify(r => r.ExtendSessionAsync(userId, query.CurrentSessionId), Times.Once);
        }
    }
}