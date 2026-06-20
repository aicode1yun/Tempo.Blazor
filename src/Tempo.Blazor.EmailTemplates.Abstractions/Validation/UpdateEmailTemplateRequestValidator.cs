using FluentValidation;
using Microsoft.Extensions.Localization;
using Tempo.Blazor.EmailTemplates.Abstractions.Dtos;
using Tempo.Blazor.EmailTemplates.Abstractions.Resources;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Validation;

/// <summary>Validates <see cref="UpdateEmailTemplateRequest"/> with localized messages.</summary>
public sealed class UpdateEmailTemplateRequestValidator : AbstractValidator<UpdateEmailTemplateRequest>
{
    /// <summary>Initializes the validator with a localized message source.</summary>
    public UpdateEmailTemplateRequestValidator(IStringLocalizer<EmailTemplateValidationResources> localizer)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(_ => localizer["Name_Required"])
            .MaximumLength(200).WithMessage(_ => localizer["Name_TooLong"]);

        RuleFor(x => x.Subject)
            .MaximumLength(300).WithMessage(_ => localizer["Subject_TooLong"]);

        RuleFor(x => x.Language)
            .Must(lang => !string.IsNullOrEmpty(lang) && LanguagePattern.Regex.IsMatch(lang))
            .WithMessage(_ => localizer["Language_Invalid"]);

        RuleFor(x => x.ContentJson)
            .NotEmpty().WithMessage(_ => localizer["ContentJson_Required"]);
    }
}
