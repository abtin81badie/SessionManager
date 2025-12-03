// File: SessionManager.Api/Controllers/AuthController.cs

using Microsoft.AspNetCore.Mvc;
using SessionManager.Api.Middleware;
using SessionManager.Application.DTOs;
using SessionManager.Application.Interfaces;
using SessionManager.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;

namespace SessionManager.Api.Controllers
{
    /// <summary>
    /// Manages User Authentication and Session Creation.
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly ISessionRepository _sessionRepository;
        private readonly IUserRepository _userRepository;
        private readonly ICryptoService _cryptoService;
        private readonly ITokenService _tokenService;

        public AuthController(
            ISessionRepository sessionRepository,
            IUserRepository userRepository,
            ICryptoService cryptoService,
            ITokenService tokenService)
        {
            _sessionRepository = sessionRepository;
            _userRepository = userRepository;
            _cryptoService = cryptoService;
            _tokenService = tokenService;
        }

        /// <summary>
        /// Authenticates a user and creates a new active session.
        /// </summary>
        /// <remarks>
        /// **Key Features:**
        /// <br/>
        /// 1. **Auto-Registration:** If the <paramref name="request"/>.Username does not exist in the database, a new user is automatically created with the provided credentials.
        /// <br/>
        /// 2. **Max-2-Device Rule:** This endpoint enforces a strict limit of 2 active sessions per user. If a 3rd session is created, the oldest session is automatically evicted via Redis Lua scripting.
        /// <br/>
        /// 3. **HATEOAS:** The response includes dynamic links to related actions (Renew, Logout).
        /// </remarks>
        /// <param name="request">The login credentials and device information.</param>
        /// <returns>A JWT Token and HATEOAS links.</returns>
        /// <response code="200">
        /// **Success.** Returns the JWT token and links.
        /// <br/>
        /// Example Links:
        /// <ul>
        /// <li>`self`: This endpoint</li>
        /// <li>`renew`: POST /api/sessions/renew</li>
        /// <li>`logout`: DELETE /api/auth/logout</li>
        /// </ul>
        /// </response>
        /// <response code="400">
        /// **Bad Request.** /// <br/>
        /// Possible reasons:
        /// <ul>
        /// <li>Username is too short (min 3 chars).</li>
        /// <li>Password is too short (min 6 chars).</li>
        /// <li>DeviceName is missing.</li>
        /// </ul>
        /// </response>
        /// <response code="401">**Unauthorized.** Invalid password for an existing user.</response>
        [HttpPost("login")]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)] // Swagger will show generic error schema
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // 1. Validation
            // Throws ArgumentException if invalid, caught by global ExceptionMiddleware
            LoginValidator.Validate(request);

            // 2. Check if user exists
            var user = await _userRepository.GetByUsernameAsync(request.Username);

            if (user == null)
            {
                // === AUTO-REGISTRATION FLOW ===
                // Encrypt the password using AES-256
                var (cipherText, iv) = _cryptoService.Encrypt(request.Password);

                user = new User
                {
                    Id = Guid.NewGuid(),
                    Username = request.Username,
                    PasswordCipherText = cipherText,
                    PasswordIV = iv,
                    Role = "User" // Default role
                };

                await _userRepository.CreateUserAsync(user);
            }
            else
            {
                // === LOGIN VERIFICATION FLOW ===
                // Decrypt stored password and compare
                var decryptedPassword = _cryptoService.Decrypt(user.PasswordCipherText, user.PasswordIV);
                if (decryptedPassword != request.Password)
                {
                    throw new UnauthorizedAccessException("Invalid username or password.");
                }
            }

            // 3. Create Session Token (Used as key in Redis)
            var sessionToken = Guid.NewGuid().ToString();

            // 4. Generate JWT (Linked to the Redis Session Token via 'jti' claim)
            var jwt = _tokenService.GenerateJwt(user, sessionToken);

            // 5. Create Session Object
            var sessionInfo = new SessionInfo
            {
                Token = sessionToken,
                UserId = user.Id,
                DeviceInfo = request.DeviceName,
                CreatedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow
            };

            // 6. Save to Redis (Max 2 Devices Rule Enforced here via Lua Script)
            // TTL is set to 1 hour
            await _sessionRepository.CreateSessionAsync(user.Id, sessionInfo, TimeSpan.FromHours(1));

            // 7. HATEOAS Response Construction
            var response = new LoginResponse
            {
                Token = jwt,
                Links = new List<Link>
                {
                    new Link("self", "/api/auth/login", "POST"),
                    new Link("renew", "/api/sessions/renew", "POST"),
                    new Link("logout", "/api/auth/logout", "DELETE"),
                    new Link("active_sessions", "/api/sessions", "GET")
                }
            };

            return Ok(response);
        }

        /// <summary>
        /// Logs out a user by invalidating their specific session token.
        /// </summary>
        /// <remarks>
        /// Removes the session data from Redis. If the token is already invalid or expired, returns 404.
        /// </remarks>
        /// <param name="request">The JWT Token to revoke.</param>
        /// <returns>Success message.</returns>
        /// <response code="200">Session successfully deleted.</response>
        /// <response code="400">Invalid Token format.</response>
        /// <response code="404">Session not found (Already logged out or expired).</response>
        [HttpDelete("logout")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)] // Added 404
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            // 1. Validate Format
            LogoutValidator.ValidateLogout(request);

            string redisSessionToken;
            Guid userId;

            // 2. Parse JWT
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(request.Token);

                var jtiClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
                var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);

                if (jtiClaim == null || subClaim == null)
                {
                    return BadRequest(new { Message = "Invalid Token: Missing Session ID (jti) or User ID (sub)." });
                }

                redisSessionToken = jtiClaim.Value;
                if (!Guid.TryParse(subClaim.Value, out userId))
                {
                    return BadRequest(new { Message = "Invalid Token: User ID is not a valid GUID." });
                }
            }
            catch (Exception)
            {
                return BadRequest(new { Message = "Invalid Token format. Could not parse JWT." });
            }

            // 3. Delete from Redis AND Check Result
            bool wasDeleted = await _sessionRepository.DeleteSessionAsync(redisSessionToken, userId);

            if (!wasDeleted)
            {
                // If false, it means the key didn't exist in Redis
                return NotFound(new { Message = "Session not found or already logged out." });
            }

            // 4. Success Response
            return Ok(new
            {
                Message = "Logged out successfully.",
                Links = new List<Link>
                {
                    new Link("login", "/api/auth/login", "POST")
                }
            });
        }
    }
}