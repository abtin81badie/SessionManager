namespace SessionManager.Application.Models;

public class TokenUserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; }
    public string Role { get; set; }
}