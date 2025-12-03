namespace SessionManager.Application.DTOs
{
    public class RenewSessionResponse
    {
        public string Message { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public List<Link> Links { get; set; } = new List<Link>();
    }
}