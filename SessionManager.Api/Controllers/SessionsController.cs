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
        /// <response code="400">Invalid request (Missing Token).</response>
        /// <response code="404">Session not found or already expired.</response>
        [HttpPost("renew")]
        [ProducesResponseType(typeof(RenewSessionResponse), StatusCodes.Status200OK)] // Updated Type
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RenewSession([FromBody] RenewSessionRequest request)
        {
            // 1. Validate Format
            SessionValidator.ValidateRenew(request);

            // 2. PARSE JWT
            string redisSessionToken;
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(request.Token);
                var jtiClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);

                if (jtiClaim == null)
                    return BadRequest(new { Message = "Invalid Token: Missing Session ID (jti)." });

                redisSessionToken = jtiClaim.Value;
            }
            catch (Exception)
            {
                return BadRequest(new { Message = "Invalid Token format. Could not parse JWT." });
            }

            // 3. LOOKUP
            var session = await _sessionRepository.GetSessionAsync(redisSessionToken);

            if (session == null)
            {
                return NotFound(new { Message = "Session not found or expired." });
            }

            // 4. EXTEND
            var userId = session.UserId;
            await _sessionRepository.ExtendSessionAsync(userId, redisSessionToken, TimeSpan.FromHours(1));

            // 5. BUILD HATEOAS RESPONSE
            var response = new RenewSessionResponse
            {
                Message = "Session renewed successfully.",
                Token = request.Token,
                Links = new List<Link>
                {
                    new Link("self", "/api/sessions/renew", "POST"),
                    new Link("logout", "/api/auth/logout", "DELETE"),
                    new Link("active_sessions", "/api/sessions", "GET")
                }
            };

            return Ok(response);
        }
    }
}