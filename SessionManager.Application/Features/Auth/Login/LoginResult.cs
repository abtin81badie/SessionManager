using SessionManager.Domain.Entities;

namespace SessionManager.Application.Features.Auth.Login
{
    public class LoginResult
    {
        public string Token { get; set; } // The JWT
        public User User { get; set; }    // The User entity (for further processing if needed)
    }
}
