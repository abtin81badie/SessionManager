using FluentValidation.TestHelper;
using SessionManager.Application.Features.Auth.Login;
using Xunit;

namespace SessionManager.Tests.Features.Auth.Login
{
    public class LoginCommandValidatorTests
    {
        private readonly LoginCommandValidator _validator;

        public LoginCommandValidatorTests()
        {
            _validator = new LoginCommandValidator();
        }

        [Fact]
        public void Should_Have_Error_When_Username_IsEmpty()
        {
            var command = new LoginCommand { Username = "", Password = "password123", DeviceName = "PC" };
            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Username).WithErrorMessage("Username is required.");
        }

        [Fact]
        public void Should_Have_Error_When_Username_IsTooShort()
        {
            var command = new LoginCommand { Username = "ab", Password = "password123", DeviceName = "PC" };
            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Username).WithErrorMessage("Username must be at least 3 characters.");
        }

        [Fact]
        public void Should_Have_Error_When_Password_IsEmpty()
        {
            var command = new LoginCommand { Username = "user", Password = "", DeviceName = "PC" };
            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Password).WithErrorMessage("Password is required.");
        }

        [Fact]
        public void Should_Have_Error_When_Password_IsTooShort()
        {
            var command = new LoginCommand { Username = "user", Password = "123", DeviceName = "PC" };
            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Password).WithErrorMessage("Password must be at least 6 characters.");
        }

        [Fact]
        public void Should_Have_Error_When_DeviceName_IsEmpty()
        {
            var command = new LoginCommand { Username = "user", Password = "password123", DeviceName = "" };
            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.DeviceName).WithErrorMessage("Device Name is required for session tracking.");
        }

        [Fact]
        public void Should_Pass_Validation_When_Command_Is_Valid()
        {
            var command = new LoginCommand { Username = "validUser", Password = "validPassword", DeviceName = "Chrome" };
            var result = _validator.TestValidate(command);
            result.ShouldNotHaveAnyValidationErrors();
        }
    }
}