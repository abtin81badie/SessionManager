using MediatR;
using SessionManager.Application.Interfaces;

namespace SessionManager.Application.Features.Sessions.Renew
{
    public class RenewSessionCommandHandler : IRequestHandler<RenewSessionCommand, bool>
    {
        private readonly ISessionRepository _sessionRepository;

        public RenewSessionCommandHandler(ISessionRepository sessionRepository)
        {
            _sessionRepository = sessionRepository;
        }

        public async Task<bool> Handle(RenewSessionCommand request, CancellationToken cancellationToken)
        {
            var session = await _sessionRepository.GetSessionAsync(request.SessionId);

            if (session == null)
            {
                return false;
            }

            await _sessionRepository.ExtendSessionAsync(request.UserId, request.SessionId);

            return true;
        }
    }
}