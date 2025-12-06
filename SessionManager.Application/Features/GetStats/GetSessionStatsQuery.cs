using MediatR;
using SessionManager.Application.DTOs;

namespace SessionManager.Application.Features.Admin.GetStats
{
    public class GetSessionStatsQuery : IRequest<SessionStatsDto>
    {
        public Guid UserId { get; set; }
        public string Role { get; set; }
        public string CurrentSessionId { get; set; }
    }
}