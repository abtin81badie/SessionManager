namespace SessionManager.Domain.Entities
{
    public class SessionInfo
    {
        public string Token { get; set; } = string.Empty;

        public Guid UserId { get; set; }

        public string DeviceInfo { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime LastActiveAt { get; set; }
    }
}
