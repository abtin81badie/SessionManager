namespace SessionManager.Infrastructure.Options;

public class JwtOptions
{
    public const string SectionName = "JwtSettings";
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "SessionManager";
    public string Audience { get; set; } = "SessionManagerClient";
    public int ExpiryMinutes { get; set; } = 60;
}