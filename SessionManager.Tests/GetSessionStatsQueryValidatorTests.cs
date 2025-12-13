using FluentValidation.TestHelper;
using SessionManager.Application.Features.Admin.GetStats;
using Xunit;

namespace SessionManager.Tests.Features.Admin.GetStats
{
    public class GetSessionStatsQueryValidatorTests
    {
        private readonly GetSessionStatsQueryValidator _validator;

        public GetSessionStatsQueryValidatorTests()
        {
            _validator = new GetSessionStatsQueryValidator();
        }

        [Fact]
        public void Should_Have_Error_When_UserId_Is_Empty()
        {
            var query = new GetSessionStatsQuery { UserId = Guid.Empty, Role = "Admin", CurrentSessionId = "abc" };
            var result = _validator.TestValidate(query);
            result.ShouldHaveValidationErrorFor(x => x.UserId).WithErrorMessage("User ID is required.");
        }

        [Fact]
        public void Should_Have_Error_When_CurrentSessionId_Is_Empty()
        {
            var query = new GetSessionStatsQuery { UserId = Guid.NewGuid(), Role = "Admin", CurrentSessionId = "" };
            var result = _validator.TestValidate(query);
            result.ShouldHaveValidationErrorFor(x => x.CurrentSessionId).WithErrorMessage("Current Session ID is required.");
        }

        [Fact]
        public void Should_Have_Error_When_Role_Is_Empty()
        {
            var query = new GetSessionStatsQuery { UserId = Guid.NewGuid(), Role = "", CurrentSessionId = "abc" };
            var result = _validator.TestValidate(query);
            result.ShouldHaveValidationErrorFor(x => x.Role).WithErrorMessage("User Role is required.");
        }

        [Fact]
        public void Should_Pass_Validation_When_Query_Is_Valid()
        {
            var query = new GetSessionStatsQuery
            {
                UserId = Guid.NewGuid(),
                Role = "User",
                CurrentSessionId = "valid-session-guid"
            };

            var result = _validator.TestValidate(query);
            result.ShouldNotHaveAnyValidationErrors();
        }
    }
}