using MediatR; // Required for IRequestHandler
using SessionManager.Application.Features.Auth.Logout;
using SessionManager.Application.Interfaces;

namespace SessionManager.Application.Features.Auth.Logout
{
    public class LogoutCommandHandler : IRequestHandler<LogoutCommand, bool>
    {
        private readonly ISessionRepository _sessionRepository;

        public LogoutCommandHandler(ISessionRepository sessionRepository)
        {
            _sessionRepository = sessionRepository;
        }

        public async Task<bool> Handle(LogoutCommand request, CancellationToken cancellationToken)
        {
            return await _sessionRepository.DeleteSessionAsync(request.SessionId, request.UserId);
        }
    }
}