using Microsoft.Extensions.Options;
using Moq;
using SessionManager.Domain.Entities;
using SessionManager.Infrastructure.Validation;
using Xunit;

namespace SessionManager.Tests.Infrastructure.Validation
{
    public class SessionValidatorTests
    {
        private readonly SessionValidator _validator;

        public SessionValidatorTests()
        {
            var options = new Mock<IOptions<SessionOptions>>();
            options.Setup(o => o.Value).Returns(new SessionOptions());
            _validator = new SessionValidator(options.Object);
        }

        [Fact]
        public void ValidateCreate_Should_Throw_When_UserId_Empty()
        {
            var session = new SessionInfo { Token = "abc" };
            Assert.Throws<ArgumentException>(() => _validator.ValidateCreate(Guid.Empty, session));
        }

        [Fact]
        public void ValidateCreate_Should_Throw_When_Session_Null()
        {
            Assert.Throws<ArgumentNullException>(() => _validator.ValidateCreate(Guid.NewGuid(), null!));
        }

        [Fact]
        public void ValidateCreate_Should_Throw_When_Token_Empty()
        {
            var session = new SessionInfo { Token = "" };
            Assert.Throws<ArgumentException>(() => _validator.ValidateCreate(Guid.NewGuid(), session));
        }

        [Fact]
        public void ValidateCreate_Should_Pass_When_Valid()
        {
            var session = new SessionInfo { Token = "valid-token" };
            _validator.ValidateCreate(Guid.NewGuid(), session); // Should not throw
        }

        [Fact]
        public void ValidateExtend_Should_Throw_When_Token_Empty()
        {
            Assert.Throws<ArgumentException>(() => _validator.ValidateExtend(Guid.NewGuid(), ""));
        }
    }
}