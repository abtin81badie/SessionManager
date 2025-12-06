using MediatR;
using SessionManager.Application.DTOs;
using SessionManager.Application.Interfaces;

namespace SessionManager.Application.Features.Admin.GetStats
{
    public class GetSessionStatsQueryHandler : IRequestHandler<GetSessionStatsQuery, SessionStatsDto>
    {
        private readonly ISessionRepository _sessionRepository;

        public GetSessionStatsQueryHandler(ISessionRepository sessionRepository)
        {
            _sessionRepository = sessionRepository;
        }

        public async Task<SessionStatsDto> Handle(GetSessionStatsQuery request, CancellationToken cancellationToken)
        {
            // 1. Validate Session Existence (Security Check)
            var currentSession = await _sessionRepository.GetSessionAsync(request.CurrentSessionId);
            if (currentSession == null)
            {
                return null; // Session expired or revoked
            }

            // 2. Determine Scope (Business Logic)
            // If Admin, they see everything (null). If User, they see only their own stats.
            Guid? targetUserId = request.Role == "Admin" ? null : request.UserId;

            // 3. Fetch Data
            var statsDto = await _sessionRepository.GetSessionStatsAsync(targetUserId);

            // 4. Mark Current Session (Logic)
            if (statsDto?.DetailedSessions != null)
            {
                foreach (var session in statsDto.DetailedSessions)
                {
                    if (session.Token == request.CurrentSessionId)
                    {
                        session.IsCurrentSession = true;
                    }
                }
            }

            // 5. Extend Session (Side Effect)
            await _sessionRepository.ExtendSessionAsync(request.UserId, request.CurrentSessionId);

            return statsDto;
        }
    }
}