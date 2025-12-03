namespace SessionManager.Application.DTOs
{
    public class SessionStatsDto
    {
        public int TotalActiveSessions { get; set; }

        // For Admin: Number of unique users currently online
        // For User: Always 1 (if active) or 0
        public int UsersOnline { get; set; }

        // You can add more fields like "AverageSessionDuration" if you track start times globally
    }
}