using FluentValidation;
using Tempo.Reporting.Abstractions.Dtos;

namespace Tempo.Reporting.Abstractions.Validation;

/// <summary>
/// Validates <see cref="CreateReportRequestDto"/> for the report-create endpoint and the portal's
/// new-report form. Like the favorites validator, messages are TmResources KEYS (resolved by the
/// caller/UI localizer, e.g. the localized report-server portal), so the validator is reusable by the
/// Blazor front end without depending on a server-side string localizer. The API host reuses the same
/// validator for a 400 response, so that body carries the resource key rather than English prose.
/// </summary>
public sealed class CreateReportRequestValidator : AbstractValidator<CreateReportRequestDto>
{
    /// <summary>Creates the validator.</summary>
    public CreateReportRequestValidator()
    {
        RuleFor(request => request.TenantId)
            .NotEmpty()
            .WithErrorCode("CreateReport.TenantId.Required")
            .WithMessage("CreateReport_TenantId_Required");

        RuleFor(request => request.FolderId)
            .NotEmpty()
            .WithErrorCode("CreateReport.FolderId.Required")
            .WithMessage("CreateReport_FolderId_Required");

        RuleFor(request => request.Name)
            .NotEmpty()
            .WithErrorCode("CreateReport.Name.Required")
            .WithMessage("CreateReport_Name_Required");

        RuleFor(request => request.DefinitionJson)
            .NotEmpty()
            .WithErrorCode("CreateReport.DefinitionJson.Required")
            .WithMessage("CreateReport_DefinitionJson_Required");
    }
}
