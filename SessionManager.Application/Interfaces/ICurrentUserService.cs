namespace SessionManager.Application.Interfaces
{
    public interface ICurrentUserService
    {
        Guid UserId { get; }
        string SessionId { get; }
        string Role { get; }
        bool IsAuthenticated { get; }
    }
}