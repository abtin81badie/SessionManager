using System;

namespace SessionManager.Application.DTOs
{
    public class TokenClaims
    {
        public Guid UserId { get; set; }
        public string SessionId { get; set; } = string.Empty; // This corresponds to 'jti'
    }
}