using SessionManager.Application.DTOs;
using SessionManager.Application.Interfaces;
using SessionManager.Domain.Entities;
using StackExchange.Redis;
using System.Text.Json;

namespace SessionManager.Infrastructure.Persistence
{
    public class RedisSessionRepository : ISessionRepository
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _db;

        // Script 1: Create Session (Max 2 Rule)
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

        // Script 2: Renew Session (Update Score & TTL)
        private const string RenewSessionScript = @"
            local userSessionKey = KEYS[1]
            local sessionDataKey = KEYS[2]
            local score = ARGV[1]
            local token = ARGV[2]
            local ttl = tonumber(ARGV[3])

            -- 1. Update the Score in the ZSET (Make it 'newest')
            -- XX means only update if element exists
            local z_updated = redis.call('ZADD', userSessionKey, 'XX', score, token)

            -- 2. Reset the TTL on the JSON data
            local s_updated = redis.call('EXPIRE', sessionDataKey, ttl)

            return z_updated
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
                currentScore, session.Token, jsonData, 2, (long)ttl.TotalSeconds
            };

            await _db.ScriptEvaluateAsync(CreateSessionScript, keys, values);
        }

        public async Task<SessionInfo?> GetSessionAsync(string token)
        {
            var data = await _db.StringGetAsync($"session:{token}");
            if (data.IsNullOrEmpty) return null;

            return JsonSerializer.Deserialize<SessionInfo>(data!);
        }

        public async Task<bool> DeleteSessionAsync(string token, Guid userId)
        {
            var userSessionKey = $"user_sessions:{userId}";
            var sessionDataKey = $"session:{token}";

            var tran = _db.CreateTransaction();
            // We capture the task to check result later, or use KeyDeleteAsync directly
            // For transactional integrity, we can't easily get the result of KeyDelete inside the transaction immediately.
            // HOWEVER, simpler approach: If KeyDelete returns false, session didn't exist.

            // Optimized logic:
            // 1. Try to delete the main session key.
            bool wasDeleted = await _db.KeyDeleteAsync(sessionDataKey);

            if (wasDeleted)
            {
                // 2. Only if successful, remove from the User's index
                await _db.SortedSetRemoveAsync(userSessionKey, token);
                return true;
            }

            return false;
        }

        public async Task ExtendSessionAsync(Guid userId, string token, TimeSpan ttl)
        {
            var userSessionKey = $"user_sessions:{userId}";
            var sessionDataKey = $"session:{token}";
            var currentScore = DateTime.UtcNow.Ticks;

            var keys = new RedisKey[] { userSessionKey, sessionDataKey };
            var values = new RedisValue[]
            {
                currentScore, token, (long)ttl.TotalSeconds
            };

            await _db.ScriptEvaluateAsync(RenewSessionScript, keys, values);
        }

        public async Task<IEnumerable<SessionInfo>> GetActiveSessionsAsync(Guid userId)
        {
            var userSessionKey = $"user_sessions:{userId}";

            // 1. Get all tokens from ZSET (Sorted by Score)
            var tokens = await _db.SortedSetRangeByRankAsync(userSessionKey);

            if (tokens.Length == 0)
                return Enumerable.Empty<SessionInfo>();

            var sessions = new List<SessionInfo>();

            // 2. Fetch details for each token
            // Optimization: We could use StringGetAsync(RedisKey[]) for a batch get,
            // but loop is simpler for now.
            foreach (var token in tokens)
            {
                var data = await _db.StringGetAsync($"session:{token}");
                if (!data.IsNullOrEmpty)
                {
                    var session = JsonSerializer.Deserialize<SessionInfo>(data!);
                    if (session != null)
                    {
                        sessions.Add(session);
                    }
                }
                else
                {
                    // Cleanup: If ZSET has token but String is gone (expired), remove from ZSET
                    await _db.SortedSetRemoveAsync(userSessionKey, token);
                }
            }

            return sessions;
        }
        public async Task<SessionStatsDto> GetSessionStatsAsync(Guid? userId)
        {
            if (userId.HasValue)
            {
                // USER REPORT: Just check their own ZSET
                var userSessionKey = $"user_sessions:{userId}";
                var count = await _db.SortedSetLengthAsync(userSessionKey);

                return new SessionStatsDto
                {
                    TotalActiveSessions = (int)count,
                    UsersOnline = count > 0 ? 1 : 0
                };
            }
            else
            {
                // ADMIN REPORT: Global Scan
                // Note: In production, avoid Keys(). Use a dedicated counter or SCAN.
                // For this implementation, we will use the Server command to scan keys.
                var server = _redis.GetServer(_redis.GetEndPoints().First());

                // Pattern to match all user session indexes
                var pattern = "user_sessions:*";
                int totalSessions = 0;
                int usersOnline = 0;

                // This iterates using SCAN internally (safe for production compared to KEYS)
                await foreach (var key in server.KeysAsync(pattern: pattern))
                {
                    usersOnline++;
                    // Count sessions in this user's ZSET
                    var userSessionCount = await _db.SortedSetLengthAsync(key);
                    totalSessions += (int)userSessionCount;
                }

                return new SessionStatsDto
                {
                    TotalActiveSessions = totalSessions,
                    UsersOnline = usersOnline
                };
            }
        }
    }
}