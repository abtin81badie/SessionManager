public class SessionOptions
{
    public const string SectionName = "SessionSettings";

    public int MaxConcurrentSessions { get; set; } = 2;

    public int SessionTimeoutMinutes { get; set; } = 60;
}