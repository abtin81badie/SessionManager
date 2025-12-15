namespace SessionManager.Application.Common
{
    public class UserSessionContext
    {
        public Guid UserId { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsAuthenticated { get; set; }
    }
}