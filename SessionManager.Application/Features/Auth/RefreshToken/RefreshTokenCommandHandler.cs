using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SessionManager.Application.DTOs;
using SessionManager.Application.Interfaces;
using SessionManager.Application.Models;
using SessionManager.Domain.Entities;
using SessionManager.Infrastructure.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SessionManager.Application.Features.Auth.RefreshToken
{
    public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, RefreshTokenResult>
    {
        private readonly ISessionRepository _sessionRepository;
        private readonly ITokenService _tokenService;
        private readonly IUserRepository _userRepository;
        private readonly RefreshTokenOptions _refreshTokenOptions;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public RefreshTokenCommandHandler(
            ISessionRepository sessionRepository,
            ITokenService tokenService,
            IUserRepository userRepository,
            IOptions<RefreshTokenOptions> refreshTokenOptions,
            IHttpContextAccessor httpContextAccessor)
        {
            _sessionRepository = sessionRepository;
            _tokenService = tokenService;
            _userRepository = userRepository;
            _refreshTokenOptions = refreshTokenOptions.Value;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<RefreshTokenResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) throw new InvalidOperationException("HttpContext is unavailable.");

            // ==============================================================================
            // 1. EXTRACT DATA FROM HEADERS
            // ==============================================================================

            // A. Get Expired Access Token (contains the 'jti' GUID we need for lookup)
            string? accessToken = context.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(accessToken) || !accessToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Missing or invalid 'Authorization' header.");
            }
            accessToken = accessToken.Substring("Bearer ".Length).Trim();

            // B. Get Refresh Token (The opaque string the client holds)
            string? clientRefreshToken = context.Request.Headers["X-Refresh-Token"].FirstOrDefault();
            if (string.IsNullOrEmpty(clientRefreshToken))
            {
                throw new UnauthorizedAccessException("Missing 'X-Refresh-Token' header.");
            }

            // ==============================================================================
            // 2. EXTRACT SESSION ID (JTI)
            // ==============================================================================
            // Since your Login logic saves the session using the GUID (jti), 
            // we MUST extract this GUID to find the Redis key.
            var sessionId = GetJtiFromToken(accessToken);

            if (string.IsNullOrEmpty(sessionId))
            {
                throw new UnauthorizedAccessException("Could not extract Session ID (jti) from expired token.");
            }

            // ==============================================================================
            // 3. RETRIEVE SESSION FROM REDIS
            // ==============================================================================
            // Lookup Key: "session:{Guid}"
            var sessionInfo = await _sessionRepository.GetSessionAsync(sessionId);

            if (sessionInfo == null)
            {
                // 
                throw new UnauthorizedAccessException("Session not found (Token might have been rotated already).");
            }

            // 4. Validate Expiration
            if (sessionInfo.LastActiveAt.AddMinutes(_refreshTokenOptions.ExpiryMinutes) < DateTime.UtcNow)
            {
                await _sessionRepository.DeleteSessionAsync(sessionId, sessionInfo.UserId);
                throw new UnauthorizedAccessException("Refresh token expired.");
            }

            // 5. User Existence
            var user = await _userRepository.GetUserByIdAsync(sessionInfo.UserId);
            if (user == null) throw new UnauthorizedAccessException("User not found.");

            // ==============================================================================
            // 6. ROTATION (Matching Login Logic)
            // ==============================================================================

            // A. Delete the OLD session (using the Old Guid)
            await _sessionRepository.DeleteSessionAsync(sessionId, user.Id);

            // B. Generate NEW Values
            //    We strictly follow the pattern used in LoginCommandHandler:
            //    1. 'sessionToken' (Guid) -> Used for Redis Key & JWT ID
            //    2. 'refreshToken' (Opaque) -> Sent to client (but not saved in DB based on your code)

            var newSessionToken = Guid.NewGuid().ToString();
            var newRefreshToken = _tokenService.GenerateRefreshToken();

            // C. Create New Access Token (using the Guid as jti)
            var userDto = new TokenUserDto
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role
            };
            var newAccessToken = _tokenService.GenerateJwt(userDto, newSessionToken);

            // D. Save New Session
            var newSessionInfo = new SessionInfo
            {
                Token = newSessionToken, // Redis Key = "session:{Guid}"
                UserId = user.Id,
                DeviceInfo = sessionInfo.DeviceInfo,
                CreatedAt = sessionInfo.CreatedAt,
                LastActiveAt = DateTime.UtcNow
            };

            await _sessionRepository.CreateSessionAsync(user.Id, newSessionInfo);

            // 7. Return Result
            return new RefreshTokenResult
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken
            };
        }

        private string? GetJtiFromToken(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(token))
                {
                    var jwt = handler.ReadJwtToken(token);
                    // Standard JWT 'jti' claim maps to the Id property
                    return jwt.Id;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}