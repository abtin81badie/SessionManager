using SessionManager.Application.DTOs;

namespace SessionManager.Application.Interfaces
{
    public interface IUserProvisioningRepository
    {
        Task<bool> ExistsByUsernameAsync(string username);
        Task CreateUserAsync(CreateAdminDto userDto);
    }
}