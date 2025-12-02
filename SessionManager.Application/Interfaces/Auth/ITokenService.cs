using SessionManager.Domain.Entities;

namespace SessionManager.Application.Interfaces
{
    public interface ITokenService
    {
        string GenerateJwt(User user, string sessionId);
    }
}