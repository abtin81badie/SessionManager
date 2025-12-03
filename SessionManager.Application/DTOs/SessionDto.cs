using System;

namespace SessionManager.Application.DTOs
{
    public class SessionDto
    {
        public string Token { get; set; } = string.Empty;
        public string DeviceInfo { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastActiveAt { get; set; }
        public bool IsCurrentSession { get; set; }
        public List<Link> Links { get; set; } = new List<Link>();
    }
}