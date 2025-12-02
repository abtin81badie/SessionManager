using StackExchange.Redis;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using SessionManager.Domain.Entities;
using SessionManager.Application.Interfaces; // This requires the ProjectReference above

namespace SessionManager.Infrastructure.Persistence
{
    public class RedisSessionRepository : ISessionRepository
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _db;

        // Note: defined as a standard string, not LuaScript object
        private const string CreateSessionScript = @"
            local userSessionKey = KEYS[1]
            local sessionDataKey = KEYS[2]
            local score = ARGV[1]
            local token = ARGV[2]
            local sessionData = ARGV[3]
            local limit = tonumber(ARGV[4])
            local ttl = tonumber(ARGV[5])

            local count = redis.call('ZCARD', userSessionKey)

            if count >= limit then
                local popped = redis.call('ZPOPMIN', userSessionKey, count - limit + 1)
                if #popped > 0 then
                   local oldToken = popped[1]
                   redis.call('DEL', 'session:' .. oldToken)
                end
            end

            redis.call('ZADD', userSessionKey, score, token)
            redis.call('SET', sessionDataKey, sessionData, 'EX', ttl)

            return 1
        ";

        public RedisSessionRepository(IConnectionMultiplexer redis)
        {
            _redis = redis;
            _db = _redis.GetDatabase();
        }

        public async Task CreateSessionAsync(Guid userId, SessionInfo session, TimeSpan ttl)
        {
            var userSessionKey = $"user_sessions:{userId}";
            var sessionDataKey = $"session:{session.Token}";

            var currentScore = DateTime.UtcNow.Ticks;
            var jsonData = JsonSerializer.Serialize(session);

            var keys = new RedisKey[] { userSessionKey, sessionDataKey };
            var values = new RedisValue[]
            {
                currentScore,
                session.Token,
                jsonData,
                2,
                (long)ttl.TotalSeconds
            };

            // FIX: Passing the string directly to the method
            await _db.ScriptEvaluateAsync(CreateSessionScript, keys, values);
        }

        public async Task<SessionInfo?> GetSessionAsync(string token)
        {
            var data = await _db.StringGetAsync($"session:{token}");
            if (data.IsNullOrEmpty) return null;

            return JsonSerializer.Deserialize<SessionInfo>(data!);
        }

        public async Task DeleteSessionAsync(string token, Guid userId)
        {
            var userSessionKey = $"user_sessions:{userId}";
            var sessionDataKey = $"session:{token}";

            var tran = _db.CreateTransaction();
            _ = tran.KeyDeleteAsync(sessionDataKey);
            _ = tran.SortedSetRemoveAsync(userSessionKey, token);
            await tran.ExecuteAsync();
        }
    }
}