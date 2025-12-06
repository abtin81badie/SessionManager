using MediatR;

namespace SessionManager.Application.Features.Sessions.Renew
{
    public class RenewSessionCommand : IRequest<bool>
    {
        public Guid UserId { get; set; }
        public string SessionId { get; set; }
    }
}