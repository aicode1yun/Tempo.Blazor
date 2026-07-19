using FluentValidation;
using Tempo.Reporting.Abstractions.Dtos;

namespace Tempo.Reporting.Abstractions.Validation;

/// <summary>
/// Validates <see cref="AddReportFavoriteRequestDto"/> for the favorites endpoint. Messages are
/// TmResources KEYS (resolved by the caller/UI localizer) so the validator is reusable by the Blazor
/// front end without depending on a server-side string localizer.
/// </summary>
public sealed class AddReportFavoriteRequestValidator : AbstractValidator<AddReportFavoriteRequestDto>
{
    /// <summary>Creates the validator.</summary>
    public AddReportFavoriteRequestValidator()
    {
        RuleFor(request => request.TenantId)
            .NotEmpty()
            .WithErrorCode("AddReportFavorite.TenantId.Required")
            .WithMessage("AddReportFavorite_TenantId_Required");

        RuleFor(request => request.ReportId)
            .NotEmpty()
            .WithErrorCode("AddReportFavorite.ReportId.Required")
            .WithMessage("AddReportFavorite_ReportId_Required");
    }
}
