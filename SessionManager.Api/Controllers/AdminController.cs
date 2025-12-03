using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SessionManager.Api.Middleware;
using SessionManager.Application.DTOs;
using SessionManager.Application.Interfaces;

namespace SessionManager.Api.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Produces("application/json")]
    public class AdminController : ControllerBase
    {
        private readonly ISessionRepository _sessionRepository;

        public AdminController(ISessionRepository sessionRepository)
        {
            _sessionRepository = sessionRepository;
        }

        /// <summary>
        /// Retrieves session statistics with HATEOAS links.
        /// </summary>
        /// <remarks>
        /// **Behavior:**
        /// <br/>
        /// - **Admin:** Returns global statistics.
        /// - **User:** Returns statistics only for the current user.
        /// </remarks>
        [HttpGet("stats")]
        [Authorize]
        [ProducesResponseType(typeof(SessionStatsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetSessionStats()
        {
            // 1. Extract Token Claims (UserId + Role + SessionId)
            var claims = SessionValidator.ValidateAndExtractClaims(Request);

            // 2. CHECK SESSION VALIDITY (New Logic)
            // Even if JWT is valid, we must ensure the session exists in Redis.
            // This prevents access if the user was force-logged out or the session expired.
            var currentSession = await _sessionRepository.GetSessionAsync(claims.SessionId);
            if (currentSession == null)
            {
                return Unauthorized(new { Message = "Session expired or revoked." });
            }

            // 3. Get Data based on Role
            SessionStatsDto statsDto;

            if (claims.Role == "Admin")
            {
                statsDto = await _sessionRepository.GetSessionStatsAsync(null);
            }
            else
            {
                statsDto = await _sessionRepository.GetSessionStatsAsync(claims.UserId);
            }

            // 4. Build HATEOAS Response
            var response = new SessionStatsResponse
            {
                TotalActiveSessions = statsDto.TotalActiveSessions,
                UsersOnline = statsDto.UsersOnline,
                Links = new List<Link>
                {
                    new Link("self", "/api/admin/stats", "GET"),
                    new Link("active_sessions", "/api/sessions", "GET"),
                    new Link("logout", "/api/auth/logout", "DELETE")
                }
            };

            return Ok(response);
        }
    }
}