using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SessionManager.Application.Common;
using SessionManager.Application.DTOs;
using SessionManager.Application.Features.Sessions.GetActive;
using SessionManager.Application.Features.Sessions.Renew;
using SessionManager.Application.Interfaces;

namespace SessionManager.Api.Controllers
{
    [ApiController]
    [Route("api/sessions")]
    [Produces("application/json")]
    public class SessionsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly UserSessionContext _userContext;

        public SessionsController(IMediator mediator, UserSessionContext userContext)
        {
            _mediator = mediator;
            _userContext = userContext;
        }

        /// <summary>
        /// Renews an active session to prevent expiration.
        /// </summary>
        /// <remarks>
        /// Updates the session's Time-To-Live (TTL) in Redis and updates its activity score
        /// to ensure it is not evicted by the "Max 2 Devices" rule.
        /// </remarks>
        /// <returns>Confirmation and HATEOAS links.</returns>
        /// <response code="200">Session successfully renewed.</response>
        /// <response code="400">Missing or Malformed Header.</response>
        /// <response code="401">Invalid Token Claims.</response>
        /// <response code="404">Session not found or already expired.</response>
        [HttpPost("renew")]
        [Authorize]
        [ProducesResponseType(typeof(RenewSessionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RenewSession()
        {
            // 1. Create Command using Service (Decoupled from HTTP)
            var command = new RenewSessionCommand
            {
                UserId = _userContext.UserId,
                SessionId = _userContext.SessionId
            };

            // 2. Dispatch
            var success = await _mediator.Send(command);

            if (!success)
            {
                return NotFound(new { Message = "Session not found or expired." });
            }

            // 3. Build Response (HATEOAS)
            // We retrieve the raw token string purely for the echo response
            var rawToken = Request.Headers["Authorization"].ToString().Replace("Bearer ", "").Trim();

            var response = new RenewSessionResponse
            {
                Message = "Session renewed successfully.",
                Token = rawToken,
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
        /// <response code="401">Invalid Token Claims or Session Expired.</response>
        [HttpGet]
        [Authorize]
        [ProducesResponseType(typeof(IEnumerable<SessionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetActiveSessions()
        {
            // 1. Create Query using Service
            var query = new GetActiveSessionsQuery
            {
                UserId = _userContext.UserId,
                CurrentSessionId = _userContext.SessionId
            };

            // 2. Dispatch
            var sessions = await _mediator.Send(query);

            // 3. Handle the "null" case from Handler (Session Invalid in Redis)
            if (sessions == null)
            {
                return Unauthorized(new { Message = "Session expired or revoked." });
            }

            // 4. Enrich with HATEOAS (Presentation Logic)
            foreach (var session in sessions)
            {
                session.Links = new List<Link>
                {
                    session.IsCurrentSession
                        ? new Link("logout", "/api/auth/logout", "DELETE")
                        : null
                };
                session.Links.RemoveAll(x => x == null); // Cleanup
            }

            return Ok(sessions);
        }
    }
}