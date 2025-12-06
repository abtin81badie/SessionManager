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
        /// Retrieves detailed session statistics.
        /// </summary>
        /// <remarks>
        /// **Privacy & Access Rules:**
        /// - **Admin:** Returns a global report of ALL active sessions and users.
        /// - **User:** Returns a report ONLY for their own active sessions.
        /// <br/>
        /// **Side Effect:** Automatically extends the current session's lifespan.
        /// </remarks>
        [HttpGet("stats")]
        [Authorize]
        [ProducesResponseType(typeof(SessionStatsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetSessionStats()
        {
            // 1. Extract Token Claims
            var claims = SessionValidator.ValidateAndExtractClaims(Request);

            // 2. Validate Session Existence in Redis
            var currentSession = await _sessionRepository.GetSessionAsync(claims.SessionId);
            if (currentSession == null)
            {
                return Unauthorized(new { Message = "Session expired or revoked." });
            }

            // 3. Determine Report Scope (The Logic)
            Guid? targetUserId;

            if (claims.Role == "Admin")
            {
                // Admin asks for "null" -> Repository fetches ALL
                targetUserId = null;
            }
            else
            {
                // Regular User -> Repository fetches ONLY this userId
                targetUserId = claims.UserId;
            }

            // 4. Fetch Data
            var statsDto = await _sessionRepository.GetSessionStatsAsync(targetUserId);

            // 5. Flag Current Session (UI Helper)
            // We loop through the results to mark which one matches the token used to make this request
            foreach (var session in statsDto.DetailedSessions)
            {
                if (session.Token == claims.SessionId)
                {
                    session.IsCurrentSession = true;
                }
            }

            // 6. Extend Session (Heartbeat)
            await _sessionRepository.ExtendSessionAsync(claims.UserId, claims.SessionId);

            // 7. Build HATEOAS Response
            var response = new SessionStatsResponse
            {
                TotalActiveSessions = statsDto.TotalActiveSessions,
                UsersOnline = statsDto.UsersOnline,
                DetailedSessions = statsDto.DetailedSessions,
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