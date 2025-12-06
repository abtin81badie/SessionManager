using Microsoft.Extensions.Options;
using SessionManager.Application.Interfaces;
using SessionManager.Infrastructure.Options; 
using System.Security.Cryptography;
using System.Text;

namespace SessionManager.Infrastructure.Services
{
    public class AesCryptoService : ICryptoService
    {
        private readonly byte[] _key;

        public AesCryptoService(IOptions<AesOptions> aesOptions)
        {
            var keyString = aesOptions.Value.Key;

            if (string.IsNullOrWhiteSpace(keyString))
            {
                throw new ArgumentNullException(nameof(aesOptions), "AES Key is missing in configuration.");
            }
            try
            {
                _key = Convert.FromBase64String(keyString);
            }
            catch (FormatException)
            {
                throw new ArgumentException("AES Key is not a valid Base64 string.");
            }

            if (_key.Length != 32)
            {
                throw new ArgumentException($"AES Key must be 32 bytes (256 bits). Current size: {_key.Length} bytes.");
            }
        }

        public (string CipherText, string IV) Encrypt(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            var plainBytes = Encoding.UTF8.GetBytes(plainText);

            // Perform encryption
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            return (Convert.ToBase64String(cipherBytes), Convert.ToBase64String(aes.IV));
        }

        public string Decrypt(string cipherText, string iv)
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = Convert.FromBase64String(iv);

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            var cipherBytes = Convert.FromBase64String(cipherText);

            // Perform decryption
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}