using MediatR;
using SessionManager.Application.DTOs;
using SessionManager.Application.Interfaces;

namespace SessionManager.Application.Features.Sessions.GetActive
{
    public class GetActiveSessionsQueryHandler : IRequestHandler<GetActiveSessionsQuery, IEnumerable<SessionDto>>
    {
        private readonly ISessionRepository _sessionRepository;

        public GetActiveSessionsQueryHandler(ISessionRepository sessionRepository)
        {
            _sessionRepository = sessionRepository;
        }

        public async Task<IEnumerable<SessionDto>> Handle(GetActiveSessionsQuery request, CancellationToken cancellationToken)
        {
            // 1. Security Check: Ensure the requester's current session is still valid in Redis
            var currentSession = await _sessionRepository.GetSessionAsync(request.CurrentSessionId);
            if (currentSession == null)
            {
                // Returning null indicates "Unauthorized" or "Session Invalid"
                return null;
            }

            // 2. Fetch all sessions for user
            var sessions = await _sessionRepository.GetActiveSessionsAsync(request.UserId);

            // 3. Side Effect: Extend the current session (Keep-Alive)
            await _sessionRepository.ExtendSessionAsync(request.UserId, request.CurrentSessionId);

            // 4. Map Domain Entities to DTOs
            // We do the mapping here so the Controller receives ready-to-use data
            return sessions.Select(s => new SessionDto
            {
                Token = s.Token,
                DeviceInfo = s.DeviceInfo,
                CreatedAt = s.CreatedAt,
                LastActiveAt = s.LastActiveAt,
                IsCurrentSession = s.Token == request.CurrentSessionId,
                // Note: Links are usually a Presentation concern, but simple boolean flags
                // like 'IsCurrentSession' belong in the DTO logic here.
                Links = new List<Link>() // Links will be populated by Controller or here depending on preference.
            });
        }
    }
}