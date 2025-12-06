using SessionManager.Application.DTOs;
using SessionManager.Domain.Entities;

namespace SessionManager.Application.Interfaces
{
    public interface ISessionRepository
    {
        // Creates a session with the "Max 2 Devices" logic
        Task CreateSessionAsync(Guid userId, SessionInfo session);

        // Retrieves a session by its token
        Task<SessionInfo?> GetSessionAsync(string token);

        // Removes a specific session (Logout)
        Task<bool> DeleteSessionAsync(string token, Guid userId);

        // Method for renew
        Task ExtendSessionAsync(Guid userId, string token);
        // Get all active sessions for a user
        Task<IEnumerable<SessionInfo>> GetActiveSessionsAsync(Guid userId);
        // Get the report base the User's role
        Task<SessionStatsDto> GetSessionStatsAsync(Guid? userId);

    }
}