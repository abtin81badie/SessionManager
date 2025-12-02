using System;
using SessionManager.Application.DTOs;

namespace SessionManager.Api.Middleware
{
    public static class LoginValidator
    {
        public static void Validate(LoginRequest request)
        {
            if (request == null)
                throw new ArgumentException("Request body cannot be null.");

            if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 3)
                throw new ArgumentException("Username must be at least 3 characters.");

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
                throw new ArgumentException("Password must be at least 6 characters.");

            if (string.IsNullOrWhiteSpace(request.DeviceName))
                throw new ArgumentException("DeviceName is required.");
        }
    }
}