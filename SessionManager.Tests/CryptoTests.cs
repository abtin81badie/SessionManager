using Microsoft.Extensions.Options; 
using Moq;
using SessionManager.Infrastructure.Options; 
using SessionManager.Infrastructure.Services;
using Xunit;

namespace SessionManager.Tests
{
    public class AesCryptoServiceTests
    {
        private readonly AesCryptoService _service;
        // A valid 32-byte Base64 key
        private readonly string _validKey = "MTIzNDU2Nzg5MDEyMzQ1Njc4OTAxMjM0NTY3ODkwMTI=";

        public AesCryptoServiceTests()
        {
            // 1. Create the Options Instance
            var aesOptions = new AesOptions
            {
                Key = _validKey
            };

            // 2. Mock IOptions<AesOptions>
            // The service expects IOptions<T>, which has a .Value property containing the actual data.
            var mockOptions = new Mock<IOptions<AesOptions>>();
            mockOptions.Setup(ap => ap.Value).Returns(aesOptions);

            // 3. Pass the mocked Options to the service
            _service = new AesCryptoService(mockOptions.Object);
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
            Assert.NotNull(cipherText);
            Assert.NotNull(iv);
            Assert.NotEqual(originalPassword, cipherText); // Should be encrypted
            Assert.Equal(originalPassword, decryptedText); // Should come back correctly
        }

        [Fact]
        public void Encrypt_ShouldReturnDifferentIVs_ForSameInput()
        {
            // Arrange
            var text = "Data";

            // Act
            var result1 = _service.Encrypt(text);
            var result2 = _service.Encrypt(text);

            // Assert
            // AES must use a random IV for every encryption to be secure
            Assert.NotEqual(result1.IV, result2.IV);
            Assert.NotEqual(result1.CipherText, result2.CipherText);
        }
    }
}