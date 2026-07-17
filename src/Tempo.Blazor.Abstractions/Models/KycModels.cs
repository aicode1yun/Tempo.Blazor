namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Kind of subject being identified by the compliance wizard.</summary>
public enum KycSubjectKind
{
    /// <summary>A natural person.</summary>
    Person = 0,

    /// <summary>A legal entity (company).</summary>
    Company = 1
}

/// <summary>Logical steps of the compliance wizard.</summary>
public enum KycWizardStep
{
    /// <summary>Subject identity (person or company).</summary>
    Subject = 0,

    /// <summary>Identification documents.</summary>
    Documents = 1,

    /// <summary>Addresses.</summary>
    Addresses = 2,

    /// <summary>Ownership structure (company subjects only).</summary>
    Ownership = 3,

    /// <summary>Declarations and consents.</summary>
    Declarations = 4,

    /// <summary>Final review and submission.</summary>
    Review = 5
}

/// <summary>Kind of identification document.</summary>
public enum KycDocumentKind
{
    /// <summary>National identity card.</summary>
    NationalId = 0,

    /// <summary>Passport.</summary>
    Passport = 1,

    /// <summary>Driving license.</summary>
    DrivingLicense = 2,

    /// <summary>Residence permit.</summary>
    ResidencePermit = 3,

    /// <summary>Commercial/company register extract.</summary>
    RegisterExtract = 4,

    /// <summary>Any other document kind.</summary>
    Other = 5
}

/// <summary>Kind of address collected by the wizard.</summary>
public enum KycAddressKind
{
    /// <summary>Permanent residence address.</summary>
    Permanent = 0,

    /// <summary>Mailing/contact address.</summary>
    Mailing = 1,

    /// <summary>Registered seat of a company.</summary>
    Registered = 2,

    /// <summary>Business/branch address.</summary>
    Business = 3
}

/// <summary>Identity fields of a natural-person subject.</summary>
public sealed class KycPersonIdentity
{
    /// <summary>Given name.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Family name.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>Date of birth.</summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Nationality (ISO country code or free text, app-defined).</summary>
    public string Nationality { get; set; } = string.Empty;

    /// <summary>Optional national identification number.</summary>
    public string? PersonalIdNumber { get; set; }

    /// <summary>Creates a deep copy.</summary>
    public KycPersonIdentity Clone() => (KycPersonIdentity)MemberwiseClone();
}

/// <summary>Identity fields of a company subject.</summary>
public sealed class KycCompanyIdentity
{
    /// <summary>Registered company name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Company registration number.</summary>
    public string RegistrationNumber { get; set; } = string.Empty;

    /// <summary>Optional legal form (e.g. "s.r.o.", "GmbH").</summary>
    public string? LegalForm { get; set; }

    /// <summary>Country of incorporation.</summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>Creates a deep copy.</summary>
    public KycCompanyIdentity Clone() => (KycCompanyIdentity)MemberwiseClone();
}

/// <summary>An identification document attached to the draft.</summary>
public sealed class KycDocument
{
    /// <summary>Stable identifier of the document row.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Document kind.</summary>
    public KycDocumentKind Kind { get; set; }

    /// <summary>Document number.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Issuing authority.</summary>
    public string IssuedBy { get; set; } = string.Empty;

    /// <summary>Issue date, when known.</summary>
    public DateOnly? IssuedOn { get; set; }

    /// <summary>Expiry date, when the document expires.</summary>
    public DateOnly? ValidUntil { get; set; }

    /// <summary>Creates a deep copy.</summary>
    public KycDocument Clone() => (KycDocument)MemberwiseClone();
}

/// <summary>An address attached to the draft.</summary>
public sealed class KycAddress
{
    /// <summary>Stable identifier of the address row.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Address kind.</summary>
    public KycAddressKind Kind { get; set; }

    /// <summary>Street and house number.</summary>
    public string Street { get; set; } = string.Empty;

    /// <summary>City.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>Postal code.</summary>
    public string PostalCode { get; set; } = string.Empty;

    /// <summary>Country (ISO country code or free text, app-defined).</summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>Creates a deep copy.</summary>
    public KycAddress Clone() => (KycAddress)MemberwiseClone();
}

/// <summary>
/// A node in the ownership structure tree. The root represents the subject itself;
/// its children are direct owners, whose own children are their owners in turn.
/// </summary>
public sealed class KycOwnershipNode
{
    /// <summary>Stable identifier of the node.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Owner display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Ownership share in percent held in the parent node (0–100).</summary>
    public decimal SharePercent { get; set; }

    /// <summary>Whether this owner is declared a beneficial owner.</summary>
    public bool IsBeneficialOwner { get; set; }

    /// <summary>Owners of this node.</summary>
    public List<KycOwnershipNode> Children { get; set; } = [];

    /// <summary>Creates a deep copy of the whole subtree.</summary>
    public KycOwnershipNode Clone()
        => new()
        {
            Id = Id,
            Name = Name,
            SharePercent = SharePercent,
            IsBeneficialOwner = IsBeneficialOwner,
            Children = Children.Select(c => c.Clone()).ToList()
        };
}

/// <summary>Declarations and consents collected on the final data step.</summary>
public sealed class KycDeclarations
{
    /// <summary>
    /// Whether the subject is politically exposed. Null means the question
    /// has not been answered yet (an explicit answer is required).
    /// </summary>
    public bool? IsPoliticallyExposed { get; set; }

    /// <summary>Declared source of funds.</summary>
    public string SourceOfFunds { get; set; } = string.Empty;

    /// <summary>Whether the subject consented to the processing of the data.</summary>
    public bool ConsentGiven { get; set; }

    /// <summary>Optional free-text note.</summary>
    public string? Note { get; set; }

    /// <summary>Creates a deep copy.</summary>
    public KycDeclarations Clone() => (KycDeclarations)MemberwiseClone();
}

/// <summary>The full state of one compliance-wizard identification draft.</summary>
public sealed class KycDraft
{
    /// <summary>Stable identifier of the draft.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Kind of the identified subject.</summary>
    public KycSubjectKind SubjectKind { get; set; }

    /// <summary>Person identity (used when <see cref="SubjectKind"/> is Person).</summary>
    public KycPersonIdentity Person { get; set; } = new();

    /// <summary>Company identity (used when <see cref="SubjectKind"/> is Company).</summary>
    public KycCompanyIdentity Company { get; set; } = new();

    /// <summary>Identification documents.</summary>
    public List<KycDocument> Documents { get; set; } = [];

    /// <summary>Addresses.</summary>
    public List<KycAddress> Addresses { get; set; } = [];

    /// <summary>Ownership structure root (company subjects; the root is the subject itself).</summary>
    public KycOwnershipNode OwnershipRoot { get; set; } = new();

    /// <summary>Declarations and consents.</summary>
    public KycDeclarations Declarations { get; set; } = new();

    /// <summary>Timestamp of the last draft save, when persisted.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Creates a deep copy of the draft.</summary>
    public KycDraft Clone()
        => new()
        {
            Id = Id,
            SubjectKind = SubjectKind,
            Person = Person.Clone(),
            Company = Company.Clone(),
            Documents = Documents.Select(d => d.Clone()).ToList(),
            Addresses = Addresses.Select(a => a.Clone()).ToList(),
            OwnershipRoot = OwnershipRoot.Clone(),
            Declarations = Declarations.Clone(),
            UpdatedAt = UpdatedAt
        };
}

/// <summary>
/// One validation failure produced by an <see cref="IKycStepValidator"/>.
/// <see cref="MessageKey"/> is a localization resource key, never display text,
/// so hosts render it through their localizer.
/// </summary>
public sealed class KycValidationError
{
    /// <summary>Creates a validation error.</summary>
    public KycValidationError(string fieldPath, string messageKey)
    {
        FieldPath = fieldPath;
        MessageKey = messageKey;
    }

    /// <summary>Dotted path of the offending field, with collection indexes (e.g. "Documents[0].Number").</summary>
    public string FieldPath { get; }

    /// <summary>Localization key of the error message.</summary>
    public string MessageKey { get; }
}

/// <summary>
/// Validates one wizard step of a <see cref="KycDraft"/>. Implementations live
/// outside the UI package (e.g. FluentValidation-based in Tempo.Blazor.FluentValidation)
/// so the core component stays validation-framework agnostic.
/// </summary>
public interface IKycStepValidator
{
    /// <summary>Returns the validation failures of <paramref name="step"/> for <paramref name="draft"/> (empty when valid).</summary>
    Task<IReadOnlyList<KycValidationError>> ValidateAsync(KycWizardStep step, KycDraft draft, CancellationToken cancellationToken = default);
}

/// <summary>Result of submitting a completed identification draft.</summary>
public sealed class KycSubmissionResult
{
    /// <summary>Whether the submission was accepted.</summary>
    public bool Success { get; init; }

    /// <summary>Identifier assigned to the accepted submission.</summary>
    public string? SubmissionId { get; init; }

    /// <summary>Rejection errors, when the submission was not accepted.</summary>
    public IReadOnlyList<KycValidationError> Errors { get; init; } = [];
}

/// <summary>Persistence backend of the compliance wizard: drafts and final submission.</summary>
public interface IKycProvider
{
    /// <summary>Loads a previously saved draft, or null when it does not exist.</summary>
    Task<KycDraft?> LoadDraftAsync(string draftId, CancellationToken cancellationToken = default);

    /// <summary>Persists the draft so the wizard can be resumed later.</summary>
    Task SaveDraftAsync(KycDraft draft, CancellationToken cancellationToken = default);

    /// <summary>Submits the completed draft for processing.</summary>
    Task<KycSubmissionResult> SubmitAsync(KycDraft draft, CancellationToken cancellationToken = default);
}
