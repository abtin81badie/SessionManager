namespace SessionManager.Infrastructure.Options;

public class AesOptions
{
    public const string SectionName = "AesSettings";
    public string Key { get; set; } = string.Empty;
}