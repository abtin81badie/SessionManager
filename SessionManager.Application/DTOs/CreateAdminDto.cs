namespace SessionManager.Application.DTOs;

public class CreateAdminDto
{
    public string Username { get; set; } = string.Empty;
    public string PasswordCipherText { get; set; } = string.Empty;
    public string PasswordIV { get; set; } = string.Empty;
    public string Role { get; set; } = "Admin";
}