namespace SessionManager.Infrastructure.Options
{
    public class RefreshTokenOptions
    {
        public const string SectionName = "RefreshTokenOptions";

        public int ExpiryMinutes { get; set; } = 10080;
    }
}