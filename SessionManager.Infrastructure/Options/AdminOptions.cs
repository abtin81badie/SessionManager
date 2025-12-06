namespace SessionManager.Infrastructure.Options;

public class AdminOptions
{
    public const string SectionName = "AdminSettings";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}