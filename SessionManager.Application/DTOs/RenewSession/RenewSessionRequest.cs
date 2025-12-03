namespace SessionManager.Application.DTOs
{
    public class RenewSessionRequest
    {
        // We need the Token to find the specific session
        public string Token { get; set; } = string.Empty;
    }
}