using SessionManager.Domain.Entities;

namespace SessionManager.Application.Interfaces
{
    public interface ISessionValidator
    {
        void ValidateCreate(Guid userId, SessionInfo session);
        void ValidateExtend(Guid userId, string token);
        void ValidateDelete(Guid userId, string token);
    }
}