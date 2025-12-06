using FluentValidation;

namespace SessionManager.Application.Features.Auth.Logout
{
    public class LogoutCommandValidator : AbstractValidator<LogoutCommand>
    {
        public LogoutCommandValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("User ID is required to perform logout.");

            RuleFor(x => x.SessionId)
                .NotEmpty().WithMessage("Session ID is required.")
                .Must(id => Guid.TryParse(id, out _)).WithMessage("Session ID must be a valid GUID format.");
        }
    }
}