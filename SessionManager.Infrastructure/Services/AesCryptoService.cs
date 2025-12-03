using Microsoft.Extensions.Configuration;
using SessionManager.Application.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace SessionManager.Infrastructure.Services
{
    public class AesCryptoService : ICryptoService
    {
        private readonly byte[] _key;

        public AesCryptoService(IConfiguration config)
        {
            // Key must be 32 bytes (256 bits) for AES-256
            var keyString = config["AesSettings:Key"]
                           ?? throw new ArgumentNullException("AesSettings:Key is missing");
            _key = Convert.FromBase64String(keyString);
        }

        public (string CipherText, string IV) Encrypt(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV();

            var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            return (Convert.ToBase64String(cipherBytes), Convert.ToBase64String(aes.IV));
        }

        public string Decrypt(string cipherText, string iv)
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = Convert.FromBase64String(iv);

            var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            var cipherBytes = Convert.FromBase64String(cipherText);
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}