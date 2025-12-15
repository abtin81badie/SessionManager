using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SessionManager.Application.Common; // Ensure this imports your UserSessionContext
using SessionManager.Application.DTOs;
using SessionManager.Application.Features.Auth.Login;
using SessionManager.Application.Features.Auth.Logout;
using SessionManager.Application.Features.Auth.RefreshToken;

namespace SessionManager.Api.Controllers
{
    /// <summary>
    /// Manages User Authentication and Session Creation via CQRS.
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;

        // REPLACEMENT: Injected UserSessionContext to access data populated by Middleware
        private readonly UserSessionContext _userContext;

        public AuthController(IMediator mediator, UserSessionContext userContext)
        {
            _mediator = mediator;
            _userContext = userContext;
        }

        /// <summary>
        /// Authenticates a user and creates a new active session.
        /// </summary>
        /// <remarks>
        /// **Key Features:**
        /// <br/>
        /// 1. **Auto-Registration:** If the <paramref name="request"/>.Username does not exist in the database, a new user is automatically created.
        /// <br/>
        /// 2. **Max-2-Device Rule:** Enforces session limits via the Application Layer.
        /// <br/>
        /// 3. **HATEOAS:** The response includes dynamic links to related actions (Renew, Logout).
        /// </remarks>
        /// <param name="request">The login credentials and device information.</param>
        /// <returns>A JWT Token and HATEOAS links.</returns>
        /// <response code="200">
        /// **Success.** Returns the JWT token and links.
        /// </response>
        /// <response code="400">
        /// **Bad Request.** Invalid input (username too short, etc).
        /// </response>
        /// <response code="401">**Unauthorized.** Invalid credentials.</response>
        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // 1. Map DTO to Command
            var command = new LoginCommand
            {
                Username = request.Username,
                Password = request.Password,
                DeviceName = request.DeviceName
            };

            // 2. Dispatch to Application Layer
            var result = await _mediator.Send(command);

            // 3. Handle Presentation (HATEOAS)
            var response = new LoginResponse
            {
                Token = result.Token,
                RefreshToken = result.RefreshToken,
                Links = new List<Link>
                {
                    new Link("self", "/api/auth/login", "POST"),
                    new Link("refresh_token", "/api/auth/refresh-token", "POST"),
                    new Link("renew", "/api/sessions/renew", "POST"),
                    new Link("logout", "/api/auth/logout", "DELETE"),
                    new Link("active_sessions", "/api/sessions", "GET")
                }
            };

            return Ok(response);
        }

        /// <summary>
        /// Refreshes an expired Access Token using a valid Refresh Token.
        /// </summary>
        /// <param name="request">The expired Access Token and the valid Refresh Token.</param>
        /// <returns>A new set of tokens.</returns>
        /// <response code="200">Tokens refreshed successfully.</response>
        /// <response code="401">Refresh token invalid or expired.</response>
        [AllowAnonymous]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken()
        {
            // We send an empty command. The Handler pulls data from headers.
            var result = await _mediator.Send(new RefreshTokenCommand());
            return Ok(result);
        }

        /// <summary>
        /// Logs out a user by invalidating their specific session token.
        /// </summary>
        /// <remarks>
        /// Sends a command to remove the session data from persistence.
        /// </remarks>
        /// <returns>Success message.</returns>
        /// <response code="200">Session successfully deleted.</response>
        /// <response code="400">Invalid Token format.</response>
        /// <response code="404">Session not found (Already logged out or expired).</response>
        [HttpDelete("logout")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Logout()
        {
            // 1. Validation: Ensure the Middleware successfully parsed the token
            if (!_userContext.IsAuthenticated)
            {
                return Unauthorized();
            }

            // 2. Extract Data using Context (Already populated by Middleware)
            var command = new LogoutCommand
            {
                UserId = _userContext.UserId,
                SessionId = _userContext.SessionId
            };

            // 3. Dispatch
            bool isDeleted = await _mediator.Send(command);

            // 4. Return correct HTTP Status based on Result
            if (!isDeleted)
            {
                return NotFound(new { Message = "Session not found or already logged out." });
            }

            return Ok(new
            {
                Message = "Logged out successfully.",
                Links = new List<Link> { new Link("login", "/api/auth/login", "POST") }
            });
        }
    }
}