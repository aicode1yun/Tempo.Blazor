using global::FluentValidation;
using global::FluentValidation.Results;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.FluentValidation.Kyc;

/// <summary>
/// FluentValidation-based <see cref="IKycStepValidator"/> for the compliance wizard.
/// Each wizard step has its own rule set; failures carry indexed field paths
/// (e.g. "Documents[0].Number") and localization keys instead of display text,
/// so the rendering component localizes them through its own localizer.
/// </summary>
public sealed class KycStepFluentValidator : IKycStepValidator
{
    private readonly Func<DateOnly> _today;
    private readonly PersonValidator _person;
    private readonly CompanyValidator _company = new();
    private readonly DocumentValidator _document;
    private readonly AddressValidator _address = new();
    private readonly OwnershipNodeValidator _ownershipNode = new();
    private readonly DeclarationsValidator _declarations = new();

    /// <summary>
    /// Creates the validator. <paramref name="today"/> supplies the reference date for
    /// expiry/birth-date rules and defaults to the current UTC date.
    /// </summary>
    public KycStepFluentValidator(Func<DateOnly>? today = null)
    {
        _today = today ?? (() => DateOnly.FromDateTime(DateTime.UtcNow));
        _person = new PersonValidator(_today);
        _document = new DocumentValidator(_today);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KycValidationError>> ValidateAsync(
        KycWizardStep step,
        KycDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var errors = new List<KycValidationError>();

        switch (step)
        {
            case KycWizardStep.Subject:
                await ValidateSubjectAsync(draft, errors, cancellationToken);
                break;
            case KycWizardStep.Documents:
                await ValidateDocumentsAsync(draft, errors, cancellationToken);
                break;
            case KycWizardStep.Addresses:
                await ValidateAddressesAsync(draft, errors, cancellationToken);
                break;
            case KycWizardStep.Ownership:
                await ValidateOwnershipAsync(draft, errors, cancellationToken);
                break;
            case KycWizardStep.Declarations:
                await ValidateDeclarationsAsync(draft, errors, cancellationToken);
                break;
            case KycWizardStep.Review:
                await ValidateSubjectAsync(draft, errors, cancellationToken);
                await ValidateDocumentsAsync(draft, errors, cancellationToken);
                await ValidateAddressesAsync(draft, errors, cancellationToken);
                await ValidateOwnershipAsync(draft, errors, cancellationToken);
                await ValidateDeclarationsAsync(draft, errors, cancellationToken);
                break;
        }

        return errors;
    }

    private async Task ValidateSubjectAsync(KycDraft draft, List<KycValidationError> errors, CancellationToken cancellationToken)
    {
        if (draft.SubjectKind == KycSubjectKind.Person)
        {
            Append(errors, "Person", await _person.ValidateAsync(draft.Person, cancellationToken));
        }
        else
        {
            Append(errors, "Company", await _company.ValidateAsync(draft.Company, cancellationToken));
        }
    }

    private async Task ValidateDocumentsAsync(KycDraft draft, List<KycValidationError> errors, CancellationToken cancellationToken)
    {
        if (draft.Documents.Count == 0)
        {
            errors.Add(new KycValidationError("Documents", "TmKycWizard_Error_DocumentsRequired"));
            return;
        }

        for (var i = 0; i < draft.Documents.Count; i++)
        {
            Append(errors, $"Documents[{i}]", await _document.ValidateAsync(draft.Documents[i], cancellationToken));
        }
    }

    private async Task ValidateAddressesAsync(KycDraft draft, List<KycValidationError> errors, CancellationToken cancellationToken)
    {
        if (draft.Addresses.Count == 0)
        {
            errors.Add(new KycValidationError("Addresses", "TmKycWizard_Error_AddressesRequired"));
            return;
        }

        for (var i = 0; i < draft.Addresses.Count; i++)
        {
            Append(errors, $"Addresses[{i}]", await _address.ValidateAsync(draft.Addresses[i], cancellationToken));
        }
    }

    private async Task ValidateOwnershipAsync(KycDraft draft, List<KycValidationError> errors, CancellationToken cancellationToken)
    {
        // Ownership structure is collected for company subjects only.
        if (draft.SubjectKind != KycSubjectKind.Company)
        {
            return;
        }

        if (draft.OwnershipRoot.Children.Count == 0)
        {
            errors.Add(new KycValidationError("OwnershipRoot.Children", "TmKycWizard_Error_OwnersRequired"));
            return;
        }

        await ValidateOwnershipNodeAsync(draft.OwnershipRoot, "OwnershipRoot", errors, cancellationToken);
    }

    private async Task ValidateOwnershipNodeAsync(
        KycOwnershipNode parent,
        string parentPath,
        List<KycValidationError> errors,
        CancellationToken cancellationToken)
    {
        if (parent.Children.Sum(c => c.SharePercent) > 100m)
        {
            errors.Add(new KycValidationError($"{parentPath}.Children", "TmKycWizard_Error_SharesExceedTotal"));
        }

        for (var i = 0; i < parent.Children.Count; i++)
        {
            var childPath = $"{parentPath}.Children[{i}]";
            Append(errors, childPath, await _ownershipNode.ValidateAsync(parent.Children[i], cancellationToken));
            await ValidateOwnershipNodeAsync(parent.Children[i], childPath, errors, cancellationToken);
        }
    }

    private async Task ValidateDeclarationsAsync(KycDraft draft, List<KycValidationError> errors, CancellationToken cancellationToken)
        => Append(errors, "Declarations", await _declarations.ValidateAsync(draft.Declarations, cancellationToken));

    private static void Append(List<KycValidationError> errors, string prefix, ValidationResult result)
    {
        foreach (var failure in result.Errors)
        {
            var path = string.IsNullOrEmpty(failure.PropertyName) ? prefix : $"{prefix}.{failure.PropertyName}";
            errors.Add(new KycValidationError(path, failure.ErrorMessage));
        }
    }

    // ── Per-model rule sets (messages are localization keys) ─────────────────

    private sealed class PersonValidator : AbstractValidator<KycPersonIdentity>
    {
        public PersonValidator(Func<DateOnly> today)
        {
            RuleFor(p => p.FirstName).NotEmpty().WithMessage("TmKycWizard_Error_FirstNameRequired");
            RuleFor(p => p.LastName).NotEmpty().WithMessage("TmKycWizard_Error_LastNameRequired");
            RuleFor(p => p.DateOfBirth).NotNull().WithMessage("TmKycWizard_Error_DateOfBirthRequired");
            RuleFor(p => p.DateOfBirth)
                .Must(d => d is null || d.Value <= today())
                .WithMessage("TmKycWizard_Error_DateOfBirthInFuture");
            RuleFor(p => p.Nationality).NotEmpty().WithMessage("TmKycWizard_Error_NationalityRequired");
        }
    }

    private sealed class CompanyValidator : AbstractValidator<KycCompanyIdentity>
    {
        public CompanyValidator()
        {
            RuleFor(c => c.Name).NotEmpty().WithMessage("TmKycWizard_Error_CompanyNameRequired");
            RuleFor(c => c.RegistrationNumber).NotEmpty().WithMessage("TmKycWizard_Error_RegistrationNumberRequired");
            RuleFor(c => c.Country).NotEmpty().WithMessage("TmKycWizard_Error_CompanyCountryRequired");
        }
    }

    private sealed class DocumentValidator : AbstractValidator<KycDocument>
    {
        public DocumentValidator(Func<DateOnly> today)
        {
            RuleFor(d => d.Number).NotEmpty().WithMessage("TmKycWizard_Error_DocumentNumberRequired");
            RuleFor(d => d.IssuedBy).NotEmpty().WithMessage("TmKycWizard_Error_DocumentIssuerRequired");
            RuleFor(d => d.ValidUntil)
                .Must(v => v is null || v.Value >= today())
                .WithMessage("TmKycWizard_Error_DocumentExpired");
        }
    }

    private sealed class AddressValidator : AbstractValidator<KycAddress>
    {
        public AddressValidator()
        {
            RuleFor(a => a.Street).NotEmpty().WithMessage("TmKycWizard_Error_StreetRequired");
            RuleFor(a => a.City).NotEmpty().WithMessage("TmKycWizard_Error_CityRequired");
            RuleFor(a => a.PostalCode).NotEmpty().WithMessage("TmKycWizard_Error_PostalCodeRequired");
            RuleFor(a => a.Country).NotEmpty().WithMessage("TmKycWizard_Error_AddressCountryRequired");
        }
    }

    private sealed class OwnershipNodeValidator : AbstractValidator<KycOwnershipNode>
    {
        public OwnershipNodeValidator()
        {
            RuleFor(n => n.Name).NotEmpty().WithMessage("TmKycWizard_Error_OwnerNameRequired");
            RuleFor(n => n.SharePercent)
                .Must(s => s > 0m && s <= 100m)
                .WithMessage("TmKycWizard_Error_ShareOutOfRange");
        }
    }

    private sealed class DeclarationsValidator : AbstractValidator<KycDeclarations>
    {
        public DeclarationsValidator()
        {
            RuleFor(d => d.IsPoliticallyExposed).NotNull().WithMessage("TmKycWizard_Error_PepAnswerRequired");
            RuleFor(d => d.SourceOfFunds).NotEmpty().WithMessage("TmKycWizard_Error_SourceOfFundsRequired");
            RuleFor(d => d.ConsentGiven).Equal(true).WithMessage("TmKycWizard_Error_ConsentRequired");
        }
    }
}
