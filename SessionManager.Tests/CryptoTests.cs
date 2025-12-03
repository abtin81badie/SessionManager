using Microsoft.Extensions.Configuration;
using Moq;
using SessionManager.Infrastructure.Services;
using Xunit;

namespace SessionManager.Tests
{
    public class AesCryptoServiceTests
    {
        private readonly AesCryptoService _service;
        private readonly string _validKey = "MTIzNDU2Nzg5MDEyMzQ1Njc4OTAxMjM0NTY3ODkwMTI="; // 32 bytes Base64

        public AesCryptoServiceTests()
        {
            // 1. Mock Configuration to provide the Key
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(c => c["AesSettings:Key"]).Returns(_validKey);

            _service = new AesCryptoService(mockConfig.Object);
        }

        [Fact]
        public void Encrypt_And_Decrypt_ShouldReturnOriginalString()
        {
            // Arrange
            string originalPassword = "SuperSecretPassword123!";

            // Act
            var (cipherText, iv) = _service.Encrypt(originalPassword);
            var decryptedText = _service.Decrypt(cipherText, iv);

            // Assert
            Assert.NotEqual(originalPassword, cipherText); // Should be encrypted
            Assert.Equal(originalPassword, decryptedText); // Should come back correctly
        }

        [Fact]
        public void Encrypt_ShouldReturnDifferentIVs_ForSameInput()
        {
            // AES should use random IVs every time, even for same text
            var text = "Data";

            var result1 = _service.Encrypt(text);
            var result2 = _service.Encrypt(text);

            Assert.NotEqual(result1.IV, result2.IV);
        }
    }
}