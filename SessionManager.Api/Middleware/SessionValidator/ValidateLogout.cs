using SessionManager.Application.DTOs;

namespace SessionManager.Api.Middleware
{
    public static class LogoutValidator
    {
        public static void ValidateLogout(LogoutRequest request)
        {
            if (request == null) throw new ArgumentException("Request body cannot be null.");

            if (string.IsNullOrWhiteSpace(request.Token))
                throw new ArgumentException("Token is required.");

            // Check for valid JWT structure (Header.Payload.Signature)
            if (request.Token.Split('.').Length != 3)
            {
                throw new ArgumentException("Invalid Token format. Expected a valid JWT.");
            }
        }
    }
}