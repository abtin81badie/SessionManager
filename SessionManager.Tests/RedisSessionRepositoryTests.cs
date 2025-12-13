using Microsoft.Extensions.Options;
using Moq;
using SessionManager.Application.Interfaces;
using SessionManager.Domain.Entities;
using SessionManager.Infrastructure.Persistence;
using StackExchange.Redis;
using System.Net;
using System.Text.Json;
using Xunit;

namespace SessionManager.Tests.Infrastructure.Persistence
{
    public class RedisSessionRepositoryTests
    {
        private readonly Mock<IConnectionMultiplexer> _mockRedis;
        private readonly Mock<IDatabase> _mockDb;
        private readonly Mock<IServer> _mockServer;
        private readonly Mock<IUserRepository> _mockUserRepo;
        private readonly Mock<ISessionValidator> _mockValidator;
        private readonly RedisSessionRepository _repository;

        public RedisSessionRepositoryTests()
        {
            _mockRedis = new Mock<IConnectionMultiplexer>();
            _mockDb = new Mock<IDatabase>();
            _mockServer = new Mock<IServer>();
            _mockUserRepo = new Mock<IUserRepository>();
            _mockValidator = new Mock<ISessionValidator>();

            // Setup Redis mocks
            _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_mockDb.Object);

            // Mock GetServer
            var endPoint = new DnsEndPoint("localhost", 6379);
            _mockRedis.Setup(r => r.GetEndPoints(It.IsAny<bool>())).Returns(new EndPoint[] { endPoint });
            _mockRedis.Setup(r => r.GetServer(It.IsAny<EndPoint>(), It.IsAny<object>())).Returns(_mockServer.Object);

            var options = new Mock<IOptions<SessionOptions>>();
            options.Setup(o => o.Value).Returns(new SessionOptions());

            _repository = new RedisSessionRepository(
                _mockRedis.Object,
                _mockUserRepo.Object,
                options.Object,
                _mockValidator.Object
            );
        }

        [Fact]
        public async Task GetSessionStatsAsync_Should_Return_Empty_If_No_Sessions()
        {
            // Arrange
            // FIX: Explicitly provided ALL arguments for KeysAsync to avoid Expression Tree error
            _mockServer.Setup(s => s.KeysAsync(
                It.IsAny<int>(),
                "user_sessions:*",
                It.IsAny<int>(),
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<CommandFlags>()))
                .Returns(GetAsyncEnumerable(new List<RedisKey>()));

            // Act
            var result = await _repository.GetSessionStatsAsync(null);

            // Assert
            Assert.Empty(result.DetailedSessions);
            Assert.Equal(0, result.TotalActiveSessions);
        }

        [Fact]
        public async Task GetSessionStatsAsync_Should_Aggregate_Data_For_Admin()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var token = "token-123";
            var session = new SessionInfo { Token = token, UserId = userId, DeviceInfo = "Web" };
            var sessionJson = JsonSerializer.Serialize(session);

            // 1. Mock Server Keys 
            var userSessionKey = new RedisKey($"user_sessions:{userId}");

            // FIX: Explicitly pass CommandFlags
            _mockServer.Setup(s => s.KeysAsync(
                It.IsAny<int>(),
                "user_sessions:*",
                It.IsAny<int>(),
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<CommandFlags>()))
                .Returns(GetAsyncEnumerable(new List<RedisKey> { userSessionKey }));

            // 2. Mock ZSET Range 
            // FIX: Explicitly pass CommandFlags.None (The missing optional arg)
            _mockDb.Setup(d => d.SortedSetRangeByRankAsync(
                userSessionKey,
                0,
                -1,
                Order.Ascending,
                CommandFlags.None))
                .ReturnsAsync(new RedisValue[] { token });

            // 3. Mock String Get
            // FIX: Explicitly pass CommandFlags.None
            _mockDb.Setup(d => d.StringGetAsync(
                It.Is<RedisKey[]>(k => k.Length == 1 && k[0] == $"session:{token}"),
                CommandFlags.None))
                .ReturnsAsync(new RedisValue[] { sessionJson });

            // 4. Mock User Repo
            var user = new User { Id = userId, Username = "TestUser", Role = "User" };
            _mockUserRepo.Setup(r => r.GetUsersByIdsAsync(It.IsAny<HashSet<Guid>>()))
                .ReturnsAsync(new List<User> { user });

            // Act
            var result = await _repository.GetSessionStatsAsync(null);

            // Assert
            Assert.Single(result.DetailedSessions);
            Assert.Equal("TestUser", result.DetailedSessions[0].UserName);
            Assert.Equal("Web", result.DetailedSessions[0].DeviceInfo);
            Assert.Equal(1, result.UsersOnline);
        }

        // Helper
        private async IAsyncEnumerable<RedisKey> GetAsyncEnumerable(IEnumerable<RedisKey> keys)
        {
            foreach (var key in keys)
            {
                yield return key;
                await Task.CompletedTask;
            }
        }
    }
}