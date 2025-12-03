using System.Collections.Generic;

namespace SessionManager.Application.DTOs
{
    public class SessionStatsResponse : SessionStatsDto
    {
        public List<Link> Links { get; set; } = new List<Link>();
    }
}