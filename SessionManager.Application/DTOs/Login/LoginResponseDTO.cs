using System.Collections.Generic;

namespace SessionManager.Application.DTOs
{
    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public List<Link> Links { get; set; } = new List<Link>();
    }
}