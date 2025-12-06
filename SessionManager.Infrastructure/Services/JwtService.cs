using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SessionManager.Application.Interfaces; 
using SessionManager.Domain.Entities;      
using SessionManager.Infrastructure.Options; 
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SessionManager.Infrastructure.Services;

public class JwtService : ITokenService
{
    private readonly JwtOptions _jwtOptions;

    public JwtService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    public string GenerateJwt(User user, string sessionId)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_jwtOptions.Secret);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, sessionId),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("username", user.Username)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),

            Expires = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes),

            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,

            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature
            )
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}