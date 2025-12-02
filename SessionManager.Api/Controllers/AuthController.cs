using Microsoft.AspNetCore.Mvc;
using SessionManager.Application.Interfaces;
using SessionManager.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace SessionManager.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")] // Tells Swagger this API returns JSON
    public class AuthController : ControllerBase
    {
        private readonly ISessionRepository _sessionRepository;

        public AuthController(ISessionRepository sessionRepository)
        {
            _sessionRepository = sessionRepository;
        }

        /// <summary>
        /// Simulates a User Login and creates a session.
        /// </summary>
        /// <remarks>
        /// This endpoint enforces the "Max 2 Devices" rule. 
        /// If a user logs in with a 3rd device, the oldest session is removed.
        /// </remarks>
        /// <param name="userId">The unique ID of the user (Guid).</param>
        /// <param name="deviceName">The name of the device (e.g., "Chrome", "iPhone").</param>
        /// <returns>A session token.</returns>
        [HttpPost("login-simulation")]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> LoginSimulation([FromQuery] Guid userId, [FromQuery] string deviceName)
        {
            if (userId == Guid.Empty || string.IsNullOrEmpty(deviceName))
            {
                return BadRequest("UserId and DeviceName are required.");
            }

            // 1. Create a dummy session
            var token = Guid.NewGuid().ToString();
            var session = new SessionInfo
            {
                Token = token,
                UserId = userId,
                DeviceInfo = deviceName,
                CreatedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow
            };

            // 2. Save to Redis (Max 2 devices rule applies here)
            await _sessionRepository.CreateSessionAsync(userId, session, TimeSpan.FromHours(1));

            // 3. Return a typed response so Swagger sees the schema
            return Ok(new LoginResponse
            {
                Token = token,
                Message = "Session created successfully."
            });
        }

        /// <summary>
        /// Retrieves session details by Token.
        /// </summary>
        /// <param name="token">The session token GUID.</param>
        /// <returns>Session details including UserID and Device Info.</returns>
        [HttpGet("session/{token}")]
        [ProducesResponseType(typeof(SessionInfo), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSession(string token)
        {
            var session = await _sessionRepository.GetSessionAsync(token);

            if (session == null)
            {
                return Unauthorized(new { Message = "Session invalid or expired" });
            }

            return Ok(session);
        }
    }

    // DTO class defined here for simplicity so Swagger can read the Schema
    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}