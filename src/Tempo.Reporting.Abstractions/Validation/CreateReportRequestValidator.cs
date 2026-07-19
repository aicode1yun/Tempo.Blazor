using FluentValidation;
using Tempo.Reporting.Abstractions.Dtos;

namespace Tempo.Reporting.Abstractions.Validation;

/// <summary>
/// Validates <see cref="CreateReportRequestDto"/> for the report-create endpoint and the portal's
/// new-report form. Unlike the favorites validator (which emits TmResources keys), the report-server
/// portal is hardcoded-English, so this validator surfaces plain English messages the portal shows
/// inline without a string localizer. The API host reuses the same validator for a 400 response.
/// </summary>
public sealed class CreateReportRequestValidator : AbstractValidator<CreateReportRequestDto>
{
    /// <summary>Creates the validator.</summary>
    public CreateReportRequestValidator()
    {
        RuleFor(request => request.TenantId)
            .NotEmpty()
            .WithErrorCode("CreateReport.TenantId.Required")
            .WithMessage("Tenant is required.");

        RuleFor(request => request.FolderId)
            .NotEmpty()
            .WithErrorCode("CreateReport.FolderId.Required")
            .WithMessage("Target folder is required.");

        RuleFor(request => request.Name)
            .NotEmpty()
            .WithErrorCode("CreateReport.Name.Required")
            .WithMessage("Report name is required.");

        RuleFor(request => request.DefinitionJson)
            .NotEmpty()
            .WithErrorCode("CreateReport.DefinitionJson.Required")
            .WithMessage("A report definition is required.");
    }
}
