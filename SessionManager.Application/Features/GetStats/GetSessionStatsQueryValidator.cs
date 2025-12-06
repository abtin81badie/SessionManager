using FluentValidation;

namespace SessionManager.Application.Features.Admin.GetStats
{
    public class GetSessionStatsQueryValidator : AbstractValidator<GetSessionStatsQuery>
    {
        public GetSessionStatsQueryValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("User ID is required.");

            RuleFor(x => x.CurrentSessionId)
                .NotEmpty().WithMessage("Current Session ID is required.");

            RuleFor(x => x.Role)
                .NotEmpty().WithMessage("User Role is required.");
        }
    }
}