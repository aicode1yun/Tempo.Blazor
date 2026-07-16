using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.FluentValidation.Kyc;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Kyc;

/// <summary>
/// FluentValidation per-step tests for <see cref="KycStepFluentValidator"/>:
/// each wizard step has its own rule set, failures carry indexed field paths and
/// localization keys (never hardcoded English), and Review aggregates every
/// applicable step for the subject kind.
/// </summary>
public class KycStepValidationTests
{
    private static readonly DateOnly Today = new(2026, 7, 15);

    private static KycStepFluentValidator Validator() => new(() => Today);

    private static async Task<IReadOnlyList<KycValidationError>> ValidateAsync(KycWizardStep step, KycDraft draft)
        => await Validator().ValidateAsync(step, draft);

    // ── Subject step ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Subject_Person_EmptyDraft_ReportsAllRequiredFields()
    {
        var errors = await ValidateAsync(KycWizardStep.Subject, new KycDraft { SubjectKind = KycSubjectKind.Person });

        errors.Select(e => e.MessageKey).Should().Contain(
        [
            "TmKycWizard_Error_FirstNameRequired",
            "TmKycWizard_Error_LastNameRequired",
            "TmKycWizard_Error_DateOfBirthRequired",
            "TmKycWizard_Error_NationalityRequired"
        ]);
        errors.Should().Contain(e => e.FieldPath == "Person.FirstName");
    }

    [Fact]
    public async Task Subject_Person_FutureDateOfBirth_IsRejected()
    {
        var draft = KycModelTests.PersonDraft();
        draft.Person.DateOfBirth = Today.AddDays(1);

        var errors = await ValidateAsync(KycWizardStep.Subject, draft);

        errors.Should().ContainSingle(e =>
            e.FieldPath == "Person.DateOfBirth" &&
            e.MessageKey == "TmKycWizard_Error_DateOfBirthInFuture");
    }

    [Fact]
    public async Task Subject_Company_EmptyIdentity_ReportsRequiredFields()
    {
        var errors = await ValidateAsync(KycWizardStep.Subject, new KycDraft { SubjectKind = KycSubjectKind.Company });

        errors.Select(e => e.MessageKey).Should().Contain(
        [
            "TmKycWizard_Error_CompanyNameRequired",
            "TmKycWizard_Error_RegistrationNumberRequired",
            "TmKycWizard_Error_CompanyCountryRequired"
        ]);
        errors.Should().NotContain(e => e.MessageKey == "TmKycWizard_Error_FirstNameRequired");
    }

    [Fact]
    public async Task Subject_ValidPerson_HasNoErrors()
    {
        (await ValidateAsync(KycWizardStep.Subject, KycModelTests.PersonDraft())).Should().BeEmpty();
    }

    // ── Documents step ───────────────────────────────────────────────────────

    [Fact]
    public async Task Documents_EmptyList_RequiresAtLeastOneDocument()
    {
        var draft = KycModelTests.PersonDraft();
        draft.Documents.Clear();

        var errors = await ValidateAsync(KycWizardStep.Documents, draft);

        errors.Should().ContainSingle(e =>
            e.FieldPath == "Documents" &&
            e.MessageKey == "TmKycWizard_Error_DocumentsRequired");
    }

    [Fact]
    public async Task Documents_MissingNumberAndIssuer_ReportIndexedFieldPaths()
    {
        var draft = KycModelTests.PersonDraft();
        draft.Documents.Add(new KycDocument { Id = "doc-2", Kind = KycDocumentKind.Passport });

        var errors = await ValidateAsync(KycWizardStep.Documents, draft);

        errors.Should().Contain(e =>
            e.FieldPath == "Documents[1].Number" &&
            e.MessageKey == "TmKycWizard_Error_DocumentNumberRequired");
        errors.Should().Contain(e =>
            e.FieldPath == "Documents[1].IssuedBy" &&
            e.MessageKey == "TmKycWizard_Error_DocumentIssuerRequired");
    }

    [Fact]
    public async Task Documents_ExpiredDocument_IsRejected()
    {
        var draft = KycModelTests.PersonDraft();
        draft.Documents[0].ValidUntil = Today.AddDays(-1);

        var errors = await ValidateAsync(KycWizardStep.Documents, draft);

        errors.Should().ContainSingle(e =>
            e.FieldPath == "Documents[0].ValidUntil" &&
            e.MessageKey == "TmKycWizard_Error_DocumentExpired");
    }

    // ── Addresses step ───────────────────────────────────────────────────────

    [Fact]
    public async Task Addresses_EmptyList_RequiresAtLeastOneAddress()
    {
        var draft = KycModelTests.PersonDraft();
        draft.Addresses.Clear();

        var errors = await ValidateAsync(KycWizardStep.Addresses, draft);

        errors.Should().ContainSingle(e =>
            e.FieldPath == "Addresses" &&
            e.MessageKey == "TmKycWizard_Error_AddressesRequired");
    }

    [Fact]
    public async Task Addresses_MissingFields_ReportIndexedFieldPaths()
    {
        var draft = KycModelTests.PersonDraft();
        draft.Addresses.Add(new KycAddress { Id = "addr-2", Kind = KycAddressKind.Mailing });

        var errors = await ValidateAsync(KycWizardStep.Addresses, draft);

        errors.Select(e => e.FieldPath).Should().Contain(
        [
            "Addresses[1].Street",
            "Addresses[1].City",
            "Addresses[1].PostalCode",
            "Addresses[1].Country"
        ]);
        errors.Should().Contain(e => e.MessageKey == "TmKycWizard_Error_StreetRequired");
        errors.Should().Contain(e => e.MessageKey == "TmKycWizard_Error_CityRequired");
    }

    // ── Ownership step ───────────────────────────────────────────────────────

    [Fact]
    public async Task Ownership_Company_WithoutOwners_IsRejected()
    {
        var draft = KycModelTests.CompanyDraft();
        draft.OwnershipRoot.Children.Clear();

        var errors = await ValidateAsync(KycWizardStep.Ownership, draft);

        errors.Should().ContainSingle(e =>
            e.FieldPath == "OwnershipRoot.Children" &&
            e.MessageKey == "TmKycWizard_Error_OwnersRequired");
    }

    [Fact]
    public async Task Ownership_ShareOutOfRange_IsRejected()
    {
        var draft = KycModelTests.CompanyDraft();
        draft.OwnershipRoot.Children[0].SharePercent = 0m;

        var errors = await ValidateAsync(KycWizardStep.Ownership, draft);

        errors.Should().Contain(e =>
            e.FieldPath == "OwnershipRoot.Children[0].SharePercent" &&
            e.MessageKey == "TmKycWizard_Error_ShareOutOfRange");
    }

    [Fact]
    public async Task Ownership_ChildrenSharesOver100_AreRejectedAtTheParentNode()
    {
        var draft = KycModelTests.CompanyDraft();
        draft.OwnershipRoot.Children[0].SharePercent = 70m;
        draft.OwnershipRoot.Children[1].SharePercent = 40m;

        var errors = await ValidateAsync(KycWizardStep.Ownership, draft);

        errors.Should().ContainSingle(e =>
            e.FieldPath == "OwnershipRoot.Children" &&
            e.MessageKey == "TmKycWizard_Error_SharesExceedTotal");
    }

    [Fact]
    public async Task Ownership_NestedOwnerWithoutName_ReportsTheNestedPath()
    {
        var draft = KycModelTests.CompanyDraft();
        draft.OwnershipRoot.Children[1].Children[0].Name = "";

        var errors = await ValidateAsync(KycWizardStep.Ownership, draft);

        errors.Should().Contain(e =>
            e.FieldPath == "OwnershipRoot.Children[1].Children[0].Name" &&
            e.MessageKey == "TmKycWizard_Error_OwnerNameRequired");
    }

    [Fact]
    public async Task Ownership_ForPersonSubject_IsNotApplicable()
    {
        var draft = KycModelTests.PersonDraft();

        (await ValidateAsync(KycWizardStep.Ownership, draft)).Should().BeEmpty();
    }

    // ── Declarations step ────────────────────────────────────────────────────

    [Fact]
    public async Task Declarations_Unanswered_ReportsAllThreeRules()
    {
        var draft = KycModelTests.PersonDraft();
        draft.Declarations = new KycDeclarations();

        var errors = await ValidateAsync(KycWizardStep.Declarations, draft);

        errors.Select(e => e.MessageKey).Should().Contain(
        [
            "TmKycWizard_Error_PepAnswerRequired",
            "TmKycWizard_Error_SourceOfFundsRequired",
            "TmKycWizard_Error_ConsentRequired"
        ]);
        errors.Should().Contain(e => e.FieldPath == "Declarations.IsPoliticallyExposed");
    }

    // ── Review step ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Review_AggregatesEveryApplicableStep()
    {
        var draft = new KycDraft { SubjectKind = KycSubjectKind.Company };

        var errors = await ValidateAsync(KycWizardStep.Review, draft);

        errors.Select(e => e.MessageKey).Should().Contain(
        [
            "TmKycWizard_Error_CompanyNameRequired",
            "TmKycWizard_Error_DocumentsRequired",
            "TmKycWizard_Error_AddressesRequired",
            "TmKycWizard_Error_OwnersRequired",
            "TmKycWizard_Error_ConsentRequired"
        ]);
    }

    [Fact]
    public async Task Review_ValidPersonDraft_HasNoErrors()
    {
        (await ValidateAsync(KycWizardStep.Review, KycModelTests.PersonDraft())).Should().BeEmpty();
    }

    [Fact]
    public async Task Review_ValidCompanyDraft_HasNoErrors()
    {
        (await ValidateAsync(KycWizardStep.Review, KycModelTests.CompanyDraft())).Should().BeEmpty();
    }

    [Fact]
    public async Task Review_ForPerson_DoesNotRequireOwners()
    {
        var errors = await ValidateAsync(KycWizardStep.Review, new KycDraft { SubjectKind = KycSubjectKind.Person });

        errors.Should().NotContain(e => e.MessageKey == "TmKycWizard_Error_OwnersRequired");
    }
}
