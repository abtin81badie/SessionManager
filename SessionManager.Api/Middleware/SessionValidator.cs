using SessionManager.Application.DTOs;

namespace SessionManager.Api.Middleware
{
    public static class SessionValidator
    {
        public static void ValidateRenew(RenewSessionRequest request)
        {
            if (request == null)
                throw new ArgumentException("Request body cannot be null.");

            if (string.IsNullOrWhiteSpace(request.Token))
                throw new ArgumentException("Token is required.");

            // Simple JWT Format Validation
            // A JWT typically has 2 dots (Header.Payload.Signature)
            if (request.Token.Split('.').Length != 3)
                throw new ArgumentException("Invalid Token format. Expected a valid JWT.");
        }
    }
}