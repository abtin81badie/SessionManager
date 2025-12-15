using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using SessionManager.Application.Common;

namespace SessionManager.Api.Middleware
{
    public class UserSessionMiddleware
    {
        private readonly RequestDelegate _next;

        public UserSessionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, UserSessionContext userContext)
        {
            // 1. Check if the pipeline successfully authenticated the user
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var user = context.User;

                // 2. Parse User ID
                var subClaim = user.FindFirst(ClaimTypes.NameIdentifier) ??
                               user.FindFirst(JwtRegisteredClaimNames.Sub);

                if (subClaim != null && Guid.TryParse(subClaim.Value, out var userId))
                {
                    userContext.UserId = userId;
                    userContext.IsAuthenticated = true;
                }

                // 3. Parse Session ID (JTI)
                var jtiClaim = user.FindFirst(JwtRegisteredClaimNames.Jti);
                if (jtiClaim != null)
                {
                    userContext.SessionId = jtiClaim.Value;
                }

                // 4. Parse Role
                userContext.Role = user.FindFirst(ClaimTypes.Role)?.Value ?? "User";
            }

            // 5. Continue pipeline
            await _next(context);
        }
    }
}