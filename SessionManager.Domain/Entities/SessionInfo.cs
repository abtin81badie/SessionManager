using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SessionManager.Domain.Entities
{
    public class SessionInfo
    {
        public string Token { get; set; } = string.Empty;

        public Guid UserId { get; set; }

        public string DeviceInfo { get; set; } = string.Empty; // e.g., "Chrome/Windows" or "Mobile App"

        public DateTime CreatedAt { get; set; }

        public DateTime LastActiveAt { get; set; }
    }
}
