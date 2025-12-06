using Microsoft.AspNetCore.Http;
using SessionManager.Application.Interfaces;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace SessionManager.Infrastructure.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        // We access the ClaimsPrincipal (User) that ASP.NET Core middleware already built for us.
        private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

        public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

        public Guid UserId
        {
            get
            {
                var subClaim = User?.FindFirst(ClaimTypes.NameIdentifier) ??
                               User?.FindFirst(JwtRegisteredClaimNames.Sub);

                if (subClaim == null || !Guid.TryParse(subClaim.Value, out var id))
                {
                    // You can choose to throw or return Guid.Empty depending on preference
                    throw new UnauthorizedAccessException("User ID claim is missing or invalid.");
                }
                return id;
            }
        }

        public string SessionId
        {
            get
            {
                var jti = User?.FindFirst(JwtRegisteredClaimNames.Jti);
                if (jti == null)
                    throw new UnauthorizedAccessException("Session ID (jti) claim is missing.");

                return jti.Value;
            }
        }

        public string Role => User?.FindFirst(ClaimTypes.Role)?.Value ?? "User";
    }
}