using Microsoft.Extensions.Options;
using Moq;
using SessionManager.Infrastructure.Options;
using SessionManager.Infrastructure.Services;
using System.Security.Cryptography;
using Xunit;

namespace SessionManager.Tests.Services
{
    public class AesCryptoServiceTests
    {
        private readonly Mock<IOptions<AesOptions>> _mockOptions;

        public AesCryptoServiceTests()
        {
            _mockOptions = new Mock<IOptions<AesOptions>>();
        }

        // --- HELPER: Generate a valid 32-byte (256-bit) Base64 Key ---
        private string GenerateValidKey()
        {
            var keyBytes = new byte[32]; // 32 bytes * 8 = 256 bits
            RandomNumberGenerator.Fill(keyBytes);
            return Convert.ToBase64String(keyBytes);
        }

        // ==========================================
        // CONSTRUCTOR VALIDATION TESTS
        // ==========================================

        [Fact]
        public void Constructor_Should_Throw_If_Key_Is_NullOrEmpty()
        {
            // Arrange
            _mockOptions.Setup(o => o.Value).Returns(new AesOptions { Key = "" });

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new AesCryptoService(_mockOptions.Object));
            Assert.Contains("AES Key is missing", ex.Message);
        }

        [Fact]
        public void Constructor_Should_Throw_If_Key_Is_Not_Valid_Base64()
        {
            // Arrange
            _mockOptions.Setup(o => o.Value).Returns(new AesOptions { Key = "Not-Base-64-@#$%" });

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => new AesCryptoService(_mockOptions.Object));
            Assert.Equal("AES Key is not a valid Base64 string.", ex.Message);
        }

        [Fact]
        public void Constructor_Should_Throw_If_Key_Length_Is_Invalid()
        {
            // Arrange
            // 16 bytes (128 bits) is valid for AES generally, but your service explicitly demands 32 bytes (256 bits)
            var shortKey = Convert.ToBase64String(new byte[16]);
            _mockOptions.Setup(o => o.Value).Returns(new AesOptions { Key = shortKey });

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => new AesCryptoService(_mockOptions.Object));
            Assert.Contains("AES Key must be 32 bytes", ex.Message);
        }

        [Fact]
        public void Constructor_Should_Succeed_With_Valid_Key()
        {
            // Arrange
            var validKey = GenerateValidKey();
            _mockOptions.Setup(o => o.Value).Returns(new AesOptions { Key = validKey });

            // Act
            var service = new AesCryptoService(_mockOptions.Object);

            // Assert
            Assert.NotNull(service);
        }

        // ==========================================
        // FUNCTIONALITY TESTS (Encrypt/Decrypt)
        // ==========================================

        [Fact]
        public void Encrypt_Decrypt_Should_Return_Original_Text()
        {
            // Arrange
            var validKey = GenerateValidKey();
            _mockOptions.Setup(o => o.Value).Returns(new AesOptions { Key = validKey });
            var service = new AesCryptoService(_mockOptions.Object);

            var originalText = "SuperSecretPassword123!";

            // Act
            var (cipherText, iv) = service.Encrypt(originalText);
            var decryptedText = service.Decrypt(cipherText, iv);

            // Assert
            Assert.NotEqual(originalText, cipherText); // Encryption actually happened
            Assert.Equal(originalText, decryptedText); // Decryption matches original
        }

        [Fact]
        public void Encrypt_Should_Produce_Different_Outputs_For_Same_Input()
        {
            // Arrange: Setup service
            var validKey = GenerateValidKey();
            _mockOptions.Setup(o => o.Value).Returns(new AesOptions { Key = validKey });
            var service = new AesCryptoService(_mockOptions.Object);

            var text = "Hello";

            // Act: Encrypt same text twice
            var result1 = service.Encrypt(text);
            var result2 = service.Encrypt(text);

            // Assert: IVs and CipherText should be different (Semantic Security)
            Assert.NotEqual(result1.IV, result2.IV);
            Assert.NotEqual(result1.CipherText, result2.CipherText);
        }

        [Fact]
        public void Decrypt_Should_Throw_If_Key_Is_Wrong()
        {
            // Arrange: Create TWO services with DIFFERENT keys
            var key1 = GenerateValidKey();
            var key2 = GenerateValidKey();

            // Service 1
            var mock1 = new Mock<IOptions<AesOptions>>();
            mock1.Setup(o => o.Value).Returns(new AesOptions { Key = key1 });
            var service1 = new AesCryptoService(mock1.Object);

            // Service 2
            var mock2 = new Mock<IOptions<AesOptions>>();
            mock2.Setup(o => o.Value).Returns(new AesOptions { Key = key2 });
            var service2 = new AesCryptoService(mock2.Object);

            var text = "Sensitive Data";

            // Act
            var (cipherText, iv) = service1.Encrypt(text);

            // Assert: Trying to decrypt with Service 2 (Wrong Key) should fail
            // Usually throws CryptographicException (Padding is invalid and cannot be removed)
            Assert.Throws<CryptographicException>(() => service2.Decrypt(cipherText, iv));
        }
    }
}