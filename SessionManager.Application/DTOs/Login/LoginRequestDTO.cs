namespace SessionManager.Application.DTOs
{
    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
    }
}