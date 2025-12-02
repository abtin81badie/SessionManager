// File: SessionManager.Infrastructure/Services/JwtService.cs

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SessionManager.Application.Interfaces;
using SessionManager.Domain.Entities;

namespace SessionManager.Infrastructure.Services
{
    public class JwtService : ITokenService
    {
        private readonly IConfiguration _config;

        public JwtService(IConfiguration config)
        {
            _config = config;
        }

        public string GenerateJwt(User user, string sessionId)
        {
            var secretKey = _config["JwtSettings:Secret"] ?? "super_secret_fallback_key_must_be_long";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, sessionId),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("username", user.Username)
            };

            var token = new JwtSecurityToken(
                issuer: "SessionManager",
                audience: "SessionManagerClient",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}