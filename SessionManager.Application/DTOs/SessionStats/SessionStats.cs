namespace SessionManager.Application.DTOs
{
    public class SessionStatsResponse
    {
        public int TotalActiveSessions { get; set; }
        public int UsersOnline { get; set; }
        public List<SessionDetailDto> DetailedSessions { get; set; } = new List<SessionDetailDto>();
        public List<Link> Links { get; set; } = new List<Link>();
    }

    public class SessionStatsDto
    {
        public int TotalActiveSessions { get; set; }
        public int UsersOnline { get; set; }
        public List<SessionDetailDto> DetailedSessions { get; set; } = new List<SessionDetailDto>();
    }

    public class SessionDetailDto
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string DeviceInfo { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastActiveAt { get; set; }
        public bool IsCurrentSession { get; set; }
    }
}