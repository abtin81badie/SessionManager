using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SessionManager.Api.Middleware;
using SessionManager.Application.DTOs;
using SessionManager.Application.Interfaces;
using System.IdentityModel.Tokens.Jwt;

namespace SessionManager.Api.Controllers
{
    [ApiController]
    [Route("api/sessions")]
    [Produces("application/json")]
    public class SessionsController : ControllerBase
    {
        private readonly ISessionRepository _sessionRepository;

        public SessionsController(ISessionRepository sessionRepository)
        {
            _sessionRepository = sessionRepository;
        }

        /// <summary>
        /// Renews an active session to prevent expiration.
        /// </summary>
        /// <remarks>
        /// Updates the session's Time-To-Live (TTL) in Redis and updates its activity score
        /// to ensure it is not evicted by the "Max 2 Devices" rule.
        /// </remarks>
        /// <param name="request">The JWT Token to renew.</param>
        /// <returns>Confirmation and HATEOAS links.</returns>
        /// <response code="200">Session successfully renewed.</response>
        ///  /// <response code="400">Missing or Malformed Header.</response>
        /// <response code="401">Invalid Token Claims.</response>
        /// <response code="404">Session not found or already expired.</response>
        [HttpPost("renew")]
        [Authorize]
        [ProducesResponseType(typeof(RenewSessionResponse), StatusCodes.Status200OK)] // Updated Type
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RenewSession()
        {
            // 1. Get claims in one line
            var claims = SessionValidator.ValidateAndExtractClaims(Request);

            // 2. LOOKUP
            var session = await _sessionRepository.GetSessionAsync(claims.SessionId);

            if (session == null)
                return NotFound(new { Message = "Session not found or expired." });

            // 3. EXTEND
            await _sessionRepository.ExtendSessionAsync(claims.UserId, claims.SessionId, TimeSpan.FromHours(1));

            // 4. BUILD HATEOAS RESPONSE
            var response = new RenewSessionResponse
            {
                Message = "Session renewed successfully.",
                Token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "").Trim(),
                Links = new List<Link>
                {
                    new Link("self", "/api/sessions/renew", "POST"),
                    new Link("logout", "/api/auth/logout", "DELETE"),
                    new Link("active_sessions", "/api/sessions", "GET")
                }
            };

            return Ok(response);
        }

        /// <summary>
        /// Retrieves all active sessions for the current user.
        /// </summary>
        /// <remarks>
        /// Requires the 'Authorization' header with a valid JWT.
        /// </remarks>
        /// <returns>List of active sessions with HATEOAS links.</returns>
        /// <response code="200">Returns list of sessions.</response>
        /// <response code="400">Missing or Malformed Header.</response>
        /// <response code="401">Invalid Token Claims.</response>
        [HttpGet]
        [Authorize]
        [ProducesResponseType(typeof(IEnumerable<SessionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)] // Added 400
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetActiveSessions()
        {
            // 1. Validate & Extract Token (Throws ArgumentException -> 400 if missing/bad format)
            var claims = SessionValidator.ValidateAndExtractClaims(Request);

            // 2. Fetch from Repository
            var sessions = await _sessionRepository.GetActiveSessionsAsync(claims.UserId);

            // 3. Map to DTO with HATEOAS
            var sessionDtos = sessions.Select(s => new SessionDto
            {
                Token = s.Token,
                DeviceInfo = s.DeviceInfo,
                CreatedAt = s.CreatedAt,
                LastActiveAt = s.LastActiveAt,
                IsCurrentSession = s.Token == claims.SessionId,
                Links = new List<Link>
                {
                    // If it's the current session, allow Logout (Revoke)
                    // Note: Ideally, the Logout endpoint should accept a Token in the body.
                    // Since our Logout API currently takes { "token": "..." } in body, 
                    // this link assumes the client will construct that request.
                    new Link("logout", "/api/auth/logout", "DELETE"),
                    
                    // You might add a specific "Kill Session" endpoint later for remote logout
                    // new Link("revoke", $"/api/sessions/{s.Token}", "DELETE") 
                }
            });

            return Ok(sessionDtos);
        }
    }
}