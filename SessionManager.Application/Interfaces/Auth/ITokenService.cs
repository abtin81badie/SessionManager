using SessionManager.Application.Models;
using SessionManager.Domain.Entities;

namespace SessionManager.Application.Interfaces
{
    public interface ITokenService
    {
        string GenerateJwt(TokenUserDto user, string sessionId);
        string GenerateRefreshToken();
    }
}