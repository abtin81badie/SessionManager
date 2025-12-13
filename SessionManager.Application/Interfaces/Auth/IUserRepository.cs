using SessionManager.Domain.Entities;

namespace SessionManager.Application.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByUsernameAsync(string username);
        Task<User?> GetUserByIdAsync(Guid userId);
        Task CreateUserAsync(User user);
        Task<List<User>> GetUsersByIdsAsync(HashSet<Guid> userIds);
    }
}