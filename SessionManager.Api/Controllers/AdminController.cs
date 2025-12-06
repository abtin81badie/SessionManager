using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SessionManager.Application.DTOs;
using SessionManager.Application.Features.Admin.GetStats;
using SessionManager.Application.Interfaces;

namespace SessionManager.Api.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Produces("application/json")]
    public class AdminController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ICurrentUserService _currentUserService;

        public AdminController(IMediator mediator, ICurrentUserService currentUserService)
        {
            _mediator = mediator;
            _currentUserService = currentUserService;
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
        /// <returns>Statistics DTO with HATEOAS links.</returns>
        /// <response code="200">Returns statistics.</response>
        /// <response code="401">Unauthorized or Session Expired.</response>
        [HttpGet("stats")]
        [Authorize]
        [ProducesResponseType(typeof(SessionStatsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetSessionStats()
        {
            // 1. Create Query using Service (Decoupled from HTTP)
            var query = new GetSessionStatsQuery
            {
                UserId = _currentUserService.UserId,
                Role = _currentUserService.Role,
                CurrentSessionId = _currentUserService.SessionId
            };

            // 2. Dispatch
            var statsDto = await _mediator.Send(query);

            // 3. Handle Invalid Session
            if (statsDto == null)
            {
                return Unauthorized(new { Message = "Session expired or revoked." });
            }

            // 4. Build HATEOAS Response
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