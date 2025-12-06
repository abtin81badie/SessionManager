using MediatR;

namespace SessionManager.Application.Features.Auth.Login
{
    public class LoginCommand : IRequest<LoginResult>
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string DeviceName { get; set; }
    }
}
