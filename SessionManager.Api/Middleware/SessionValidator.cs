using SessionManager.Application.DTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SessionManager.Api.Middleware
{
    public class SessionValidator
    {
        public static TokenClaims ValidateAndExtractClaims(HttpRequest request)
        {
            if (!request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                throw new ArgumentException("Missing Authorization Header.");
            }

            var token = authHeader.ToString().Replace("Bearer ", "").Trim();

            if (string.IsNullOrWhiteSpace(token) || token.Split('.').Length != 3)
            {
                throw new ArgumentException("Invalid Authorization Token format.");
            }

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
                var jtiClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
                // Role can be "role" or ClaimTypes.Role depending on issuer
                var roleClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "role" || c.Type == ClaimTypes.Role);

                if (subClaim == null || jtiClaim == null || roleClaim == null)
                {
                    throw new UnauthorizedAccessException("Invalid Token: Missing required claims (sub, jti, or role).");
                }

                if (!Guid.TryParse(subClaim.Value, out Guid userId))
                {
                    throw new UnauthorizedAccessException("Invalid Token: User ID is not a valid GUID.");
                }

                return new TokenClaims
                {
                    UserId = userId,
                    SessionId = jtiClaim.Value,
                    Role = roleClaim.Value // Extracted Role
                };
            }
            catch (Exception ex) when (ex is not UnauthorizedAccessException && ex is not ArgumentException)
            {
                throw new ArgumentException("Invalid Token format. Could not parse JWT.");
            }
        }
    }
}
