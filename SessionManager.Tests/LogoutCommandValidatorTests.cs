using FluentValidation.TestHelper;
using SessionManager.Application.Features.Auth.Logout;
using Xunit;

namespace SessionManager.Tests.Features.Auth.Logout
{
    public class LogoutCommandValidatorTests
    {
        private readonly LogoutCommandValidator _validator;

        public LogoutCommandValidatorTests()
        {
            _validator = new LogoutCommandValidator();
        }

        [Fact]
        public void Should_Have_Error_When_UserId_Is_Empty()
        {
            // Arrange
            var command = new LogoutCommand
            {
                UserId = Guid.Empty, // Invalid
                SessionId = Guid.NewGuid().ToString()
            };

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId)
                  .WithErrorMessage("User ID is required to perform logout.");
        }

        [Fact]
        public void Should_Have_Error_When_SessionId_Is_Null_Or_Empty()
        {
            // Arrange
            var command = new LogoutCommand
            {
                UserId = Guid.NewGuid(),
                SessionId = string.Empty // Invalid
            };

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.SessionId)
                  .WithErrorMessage("Session ID is required.");
        }

        [Fact]
        public void Should_Have_Error_When_SessionId_Is_Not_Guid_Format()
        {
            // Arrange
            var command = new LogoutCommand
            {
                UserId = Guid.NewGuid(),
                SessionId = "invalid-guid-string" // Invalid Format
            };

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.SessionId)
                  .WithErrorMessage("Session ID must be a valid GUID format.");
        }

        [Fact]
        public void Should_Pass_Validation_When_Command_Is_Valid()
        {
            // Arrange
            var command = new LogoutCommand
            {
                UserId = Guid.NewGuid(),
                SessionId = Guid.NewGuid().ToString() // Valid Guid String
            };

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }
    }
}