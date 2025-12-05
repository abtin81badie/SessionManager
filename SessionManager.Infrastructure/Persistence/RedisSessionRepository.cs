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
        private readonly IUserRepository _userRepository;


        // ----------------------------------------------------------------------
        // LUA SCRIPTS (Atomic Operations)
        // ----------------------------------------------------------------------

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

        // Script 2: Delete Session
        private const string DeleteSessionScript = @"
        -- 1. Try to delete the session data
        local deleted = redis.call('DEL', KEYS[1])

        -- 2. If it was deleted (result is 1), remove it from the index
        if deleted == 1 then
            redis.call('ZREM', KEYS[2], ARGV[1])
        end

        -- 3. Return true (1) or false (0)
        return deleted
        ";

        // Script 3: Renew Session (Update Score & TTL)
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

        // ----------------------------------------------------------------------
        // CONSTRUCTOR
        // ----------------------------------------------------------------------
        public RedisSessionRepository(IConnectionMultiplexer redis, IUserRepository userRepository)
        {
            _redis = redis;
            _db = _redis.GetDatabase();
            _userRepository = userRepository;
        }

        // ----------------------------------------------------------------------
        // PUBLIC METHODS
        // ----------------------------------------------------------------------

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

            await _db.ScriptEvaluateAsync(
                CreateSessionScript,
                keys,
                values);
        }

        public async Task<SessionInfo?> GetSessionAsync(string token)
        {
            var data = await _db.StringGetAsync($"session:{token}");
            if (data.IsNullOrEmpty) return null;

            return JsonSerializer.Deserialize<SessionInfo>(data!);
        }

        public async Task<bool> DeleteSessionAsync(string token, Guid userId)
        {
            // 1. Prepare the Keys
            var sessionDataKey = $"session:{token}";        // KEYS[1]
            var userSessionKey = $"user_sessions:{userId}"; // KEYS[2]

            // 2. Prepare the Arguments
            var keys = new RedisKey[] { sessionDataKey, userSessionKey };
            var values = new RedisValue[] { token };

            // 3. Execute Atomically
            var result = await _db.ScriptEvaluateAsync(
                DeleteSessionScript,
                keys,
                values
            );

            // 4. Return Result
            // Redis returns 1 (integer) for true, 0 for false.
            return (int)result == 1;
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

            await _db.ScriptEvaluateAsync(
                RenewSessionScript,
                keys,
                values);
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
            var stats = new SessionStatsDto();
            var sessionKeysToFetch = new List<RedisKey>();
            var uniqueUserIds = new HashSet<Guid>();

            // 1. Gather keys from Redis
            if (userId.HasValue)
            {
                var userSessionIndex = $"user_sessions:{userId}";
                var tokens = await _db.SortedSetRangeByRankAsync(userSessionIndex);
                foreach (var token in tokens) sessionKeysToFetch.Add($"session:{token}");
            }
            else
            {
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                await foreach (var key in server.KeysAsync(pattern: "user_sessions:*"))
                {
                    var tokens = await _db.SortedSetRangeByRankAsync(key);
                    foreach (var token in tokens) sessionKeysToFetch.Add($"session:{token}");
                }
            }

            // 2. Fetch Session Data (MGET)
            var activeSessions = new List<SessionInfo>();
            if (sessionKeysToFetch.Any())
            {
                var redisValues = await _db.StringGetAsync(sessionKeysToFetch.ToArray());
                foreach (var value in redisValues)
                {
                    if (value.HasValue)
                    {
                        var session = JsonSerializer.Deserialize<SessionInfo>(value!);
                        if (session != null)
                        {
                            activeSessions.Add(session);
                            uniqueUserIds.Add(session.UserId);
                        }
                    }
                }
            }

            // 3. Fetch User Data (Using injected Repository)
            var users = await _userRepository.GetUsersByIdsAsync(uniqueUserIds);

            // 4. Map to DTO
            foreach (var session in activeSessions)
            {
                var user = users.FirstOrDefault(u => u.Id == session.UserId)
                           ?? new User { Username = "Unknown", Role = "N/A" };

                stats.DetailedSessions.Add(new SessionDetailDto
                {
                    UserId = user.Id,
                    UserName = user.Username,
                    Role = user.Role,
                    Token = session.Token,
                    DeviceInfo = session.DeviceInfo,
                    CreatedAt = session.CreatedAt,
                    LastActiveAt = session.LastActiveAt,
                    IsCurrentSession = false
                });
            }

            // 5. Aggregates
            stats.TotalActiveSessions = stats.DetailedSessions.Count;
            stats.UsersOnline = stats.DetailedSessions.Select(s => s.UserId).Distinct().Count();

            return stats;
        }
    }
}