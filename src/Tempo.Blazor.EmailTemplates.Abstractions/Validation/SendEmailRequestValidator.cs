using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Localization;
using Tempo.Blazor.EmailTemplates.Abstractions.Dtos;
using Tempo.Blazor.EmailTemplates.Abstractions.Resources;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Validation;

/// <summary>Validates <see cref="SendEmailRequest"/> with localized messages.</summary>
public sealed class SendEmailRequestValidator : AbstractValidator<SendEmailRequest>
{
    /// <summary>Initializes the validator with a localized message source.</summary>
    public SendEmailRequestValidator(IStringLocalizer<EmailTemplateValidationResources> localizer)
    {
        RuleFor(x => x.To)
            .NotEmpty().WithMessage(_ => localizer["To_Required"]);

        RuleForEach(x => x.To)
            .EmailAddress().WithMessage(_ => localizer["To_InvalidEmail"]);

        RuleForEach(x => x.Cc)
            .EmailAddress().WithMessage(_ => localizer["Cc_InvalidEmail"]);

        RuleFor(x => x.VariablesJson)
            .Must(IsValidJson).WithMessage(_ => localizer["VariablesJson_Invalid"]);
    }

    private static bool IsValidJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return true;
        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
