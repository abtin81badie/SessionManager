using MediatR;
using SessionManager.Application.DTOs;

namespace SessionManager.Application.Features.Sessions.GetActive
{
    public class GetActiveSessionsQuery : IRequest<IEnumerable<SessionDto>>
    {
        public Guid UserId { get; set; }
        public string CurrentSessionId { get; set; } // Needed to mark "IsCurrentSession"
    }
}