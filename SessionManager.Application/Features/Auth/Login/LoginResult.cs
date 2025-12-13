using SessionManager.Domain.Entities;

namespace SessionManager.Application.Features.Auth.Login
{
    public class LoginResult
    {
        public string Token { get; set; }
        public string RefreshToken { get; set; }
        public User User { get; set; } 
    }
}
