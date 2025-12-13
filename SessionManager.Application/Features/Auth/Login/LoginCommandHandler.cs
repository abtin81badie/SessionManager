using MediatR;
using SessionManager.Application.Interfaces;
using SessionManager.Application.Models;
using SessionManager.Domain.Entities;

namespace SessionManager.Application.Features.Auth.Login
{
    public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
    {
        private readonly IUserRepository _userRepository;
        private readonly ISessionRepository _sessionRepository;
        private readonly ICryptoService _cryptoService;
        private readonly ITokenService _tokenService;

        public LoginCommandHandler(
            IUserRepository userRepository,
            ISessionRepository sessionRepository,
            ICryptoService cryptoService,
            ITokenService tokenService)
        {
            _userRepository = userRepository;
            _sessionRepository = sessionRepository;
            _cryptoService = cryptoService;
            _tokenService = tokenService;
        }

        public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            // 1. User Logic (Validation is already done by the Pipeline)
            var user = await _userRepository.GetByUsernameAsync(request.Username);

            if (user == null)
            {
                // Auto-register
                var (cipherText, iv) = _cryptoService.Encrypt(request.Password);
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Username = request.Username,
                    PasswordCipherText = cipherText,
                    PasswordIV = iv,
                    Role = "User"
                };
                await _userRepository.CreateUserAsync(user);
            }
            else
            {
                // Verify Password
                var decryptedPassword = _cryptoService.Decrypt(user.PasswordCipherText, user.PasswordIV);
                if (decryptedPassword != request.Password)
                    throw new UnauthorizedAccessException("Invalid username or password.");
            }

            // 2. Session Logic
            var sessionToken = Guid.NewGuid().ToString();

            // Generate the Secure Refresh Token (Opaque String)
            var refreshToken = _tokenService.GenerateRefreshToken();

            var userDto = new TokenUserDto
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role
            };

            var jwt = _tokenService.GenerateJwt(userDto, sessionToken);

            var sessionInfo = new SessionInfo
            {
                Token = sessionToken,
                UserId = user.Id,
                DeviceInfo = request.DeviceName,
                CreatedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow
            };

            await _sessionRepository.CreateSessionAsync(user.Id, sessionInfo);

            return new LoginResult { Token = jwt, RefreshToken = refreshToken ,User = user };
        }
    }
}