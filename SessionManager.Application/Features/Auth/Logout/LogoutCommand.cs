using MediatR;

namespace SessionManager.Application.Features.Auth.Logout
{
    public class LogoutCommand : IRequest<bool>
    {
        public Guid UserId { get; set; }
        public string SessionId { get; set; }
    }
}
