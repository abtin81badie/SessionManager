using MediatR;

namespace SessionManager.Application.Features.Auth.RefreshToken
{
    public class RefreshTokenCommand : IRequest<RefreshTokenResult>
    {
    }
}