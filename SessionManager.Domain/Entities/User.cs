namespace SessionManager.Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Username { get; set; } = string.Empty;

        // Requirements specify AES Encryption, so we store CipherText, not a Hash.
        public string PasswordCipherText { get; set; } = string.Empty;

        // Initialization Vector (IV) is required for AES decryption.
        public string PasswordIV { get; set; } = string.Empty;

        // Role for RBAC (e.g., "Admin", "User")
        public string Role { get; set; } = "User";
    }
}
