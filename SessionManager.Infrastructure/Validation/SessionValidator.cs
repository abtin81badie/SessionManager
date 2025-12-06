using Microsoft.Extensions.Options;
using SessionManager.Application.DTOs;
using SessionManager.Application.Interfaces;
using SessionManager.Domain.Entities;

namespace SessionManager.Infrastructure.Validation
{
    public class SessionValidator : ISessionValidator
    {
        private readonly SessionOptions _options;

        public SessionValidator(IOptions<SessionOptions> options)
        {
            _options = options.Value;
        }

        public void ValidateCreate(Guid userId, SessionInfo session)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));

            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (string.IsNullOrWhiteSpace(session.Token))
                throw new ArgumentException("Session token is required.", nameof(session.Token));

            // Example: Enforce a specialized rule cleanly separated from DB logic
            if (session.DeviceInfo != null && session.DeviceInfo.Length > 500)
                throw new ArgumentException("Device Info is too long.");
        }

        public void ValidateExtend(Guid userId, string token)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));

            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token cannot be empty.", nameof(token));
        }

        public void ValidateDelete(Guid userId, string token)
        {
            // Similar logic for delete
            if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Token required");
        }
    }
}