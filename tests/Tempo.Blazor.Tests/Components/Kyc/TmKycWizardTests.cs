using Bunit;
using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Compliance;
using Tempo.Blazor.FluentValidation.Kyc;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Kyc;

/// <summary>
/// bUnit tests for TmKycWizard: step composition per subject kind, per-step
/// validation gating via IKycStepValidator, draft persistence, list editors
/// (documents, addresses, ownership tree) and final submission.
/// </summary>
public class TmKycWizardTests : LocalizationTestBase
{
    private static readonly DateOnly Today = new(2026, 7, 15);

    private IRenderedComponent<TmKycWizard> Render(
        IKycProvider provider,
        Action<Bunit.ComponentParameterCollectionBuilder<TmKycWizard>>? configure = null)
        => RenderComponent<TmKycWizard>(p =>
        {
            p.Add(x => x.Provider, provider);
            configure?.Invoke(p);
        });

    private static void FillPersonSubject(IRenderedComponent<TmKycWizard> cut)
    {
        cut.Find("[data-testid='kyc-first-name']").Change("Bedřich");
        cut.Find("[data-testid='kyc-last-name']").Change("Novák");
        cut.Find("[data-testid='kyc-birth-date']").Change("1980-05-12");
        cut.Find("[data-testid='kyc-nationality']").Change("CZ");
    }

    private static void ClickNext(IRenderedComponent<TmKycWizard> cut)
        => cut.Find("[data-testid='kyc-next']").Click();

    // ── Step composition ─────────────────────────────────────────────────────

    [Fact]
    public void RendersSubjectStepFirst_WithPersonStepsInTheStepper()
    {
        var cut = Render(new InMemoryKycProvider());

        cut.Find("[data-testid='kyc-wizard']");
        cut.Find("[data-testid='kyc-step-subject']");
        // Person path: Subject, Documents, Addresses, Declarations, Review.
        cut.FindAll(".tm-stepper-item").Should().HaveCount(5);
    }

    [Fact]
    public void SwitchingToCompany_AddsTheOwnershipStep()
    {
        var cut = Render(new InMemoryKycProvider());

        cut.Find("[data-testid='kyc-kind-company']").Change(true);

        cut.FindAll(".tm-stepper-item").Should().HaveCount(6);
        cut.Find("[data-testid='kyc-company-name']");
    }

    [Fact]
    public void InitialSubjectKindCompany_ShowsCompanyFields()
    {
        var cut = Render(new InMemoryKycProvider(), p => p.Add(x => x.InitialSubjectKind, KycSubjectKind.Company));

        cut.Find("[data-testid='kyc-company-name']");
        cut.FindAll(".tm-stepper-item").Should().HaveCount(6);
    }

    // ── Navigation & validation gating ───────────────────────────────────────

    [Fact]
    public void Next_WithoutValidator_Advances()
    {
        var cut = Render(new InMemoryKycProvider());

        ClickNext(cut);

        cut.Find("[data-testid='kyc-step-documents']");
    }

    [Fact]
    public void Next_WithValidatorAndEmptySubject_StaysAndShowsLocalizedErrors()
    {
        var cut = Render(new InMemoryKycProvider(), p => p.Add(x => x.StepValidator, new KycStepFluentValidator(() => Today)));

        ClickNext(cut);

        cut.Find("[data-testid='kyc-step-subject']");
        var alert = cut.Find("[data-testid='kyc-errors']");
        alert.GetAttribute("role").Should().Be("alert");
        alert.TextContent.Should().Contain("First name is required");
    }

    [Fact]
    public void Next_WithValidatorAndValidSubject_AdvancesAndClearsErrors()
    {
        var cut = Render(new InMemoryKycProvider(), p => p.Add(x => x.StepValidator, new KycStepFluentValidator(() => Today)));

        ClickNext(cut);
        cut.FindAll("[data-testid='kyc-errors']").Should().NotBeEmpty();

        FillPersonSubject(cut);
        ClickNext(cut);

        cut.Find("[data-testid='kyc-step-documents']");
        cut.FindAll("[data-testid='kyc-errors']").Should().BeEmpty();
    }

    [Fact]
    public void FieldLevelError_IsRenderedNextToTheOffendingInput()
    {
        var cut = Render(new InMemoryKycProvider(), p => p.Add(x => x.StepValidator, new KycStepFluentValidator(() => Today)));

        ClickNext(cut);

        var fieldErrors = cut.FindAll("[data-testid='kyc-field-error']");
        fieldErrors.Should().Contain(e => e.TextContent.Contains("First name is required"));
    }

    [Fact]
    public void Back_ReturnsToThePreviousStep()
    {
        var cut = Render(new InMemoryKycProvider());

        ClickNext(cut);
        cut.Find("[data-testid='kyc-back']").Click();

        cut.Find("[data-testid='kyc-step-subject']");
    }

    [Fact]
    public void OnStepChanged_FiresWithTheNewStep()
    {
        var steps = new List<KycWizardStep>();
        var cut = Render(new InMemoryKycProvider(), p => p.Add(x => x.OnStepChanged, (KycWizardStep s) => steps.Add(s)));

        ClickNext(cut);

        steps.Should().ContainSingle().Which.Should().Be(KycWizardStep.Documents);
    }

    // ── Draft persistence ────────────────────────────────────────────────────

    [Fact]
    public async Task SaveDraft_PersistsThroughTheProvider_AndShowsConfirmation()
    {
        var provider = new InMemoryKycProvider();
        KycDraft? saved = null;
        var cut = Render(provider, p => p.Add(x => x.OnDraftSaved, (KycDraft d) => saved = d));

        cut.Find("[data-testid='kyc-first-name']").Change("Bedřich");
        await cut.Find("[data-testid='kyc-save-draft']").ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='kyc-draft-saved']");
            saved.Should().NotBeNull();
        });
        (await provider.LoadDraftAsync(saved!.Id))!.Person.FirstName.Should().Be("Bedřich");
    }

    [Fact]
    public void DraftId_LoadsTheExistingDraftIntoTheForm()
    {
        var provider = new InMemoryKycProvider();
        provider.SaveDraftAsync(KycModelTests.PersonDraft("draft-42")).GetAwaiter().GetResult();

        var cut = Render(provider, p => p.Add(x => x.DraftId, "draft-42"));

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='kyc-first-name']").GetAttribute("value").Should().Be("Bedřich"));
    }

    [Fact]
    public void DraftId_ForCompanyDraft_RestoresTheCompanyStepSet()
    {
        var provider = new InMemoryKycProvider();
        provider.SaveDraftAsync(KycModelTests.CompanyDraft("draft-co")).GetAwaiter().GetResult();

        var cut = Render(provider, p => p.Add(x => x.DraftId, "draft-co"));

        cut.WaitForAssertion(() => cut.FindAll(".tm-stepper-item").Should().HaveCount(6));
    }

    // ── List editors ─────────────────────────────────────────────────────────

    [Fact]
    public void Documents_AddAndRemoveRows()
    {
        var cut = Render(new InMemoryKycProvider());
        ClickNext(cut);

        cut.Find("[data-testid='kyc-add-document']").Click();
        cut.Find("[data-testid='kyc-add-document']").Click();
        cut.FindAll("[data-testid='kyc-doc-row']").Should().HaveCount(2);

        cut.FindAll("[data-testid='kyc-doc-remove']")[0].Click();
        cut.FindAll("[data-testid='kyc-doc-row']").Should().HaveCount(1);
    }

    [Fact]
    public void Ownership_AddOwner_AndNestedChild()
    {
        var cut = Render(new InMemoryKycProvider(), p => p.Add(x => x.InitialSubjectKind, KycSubjectKind.Company));

        // Subject → Documents → Addresses → Ownership.
        ClickNext(cut);
        ClickNext(cut);
        ClickNext(cut);
        cut.Find("[data-testid='kyc-step-ownership']");

        cut.Find("[data-testid='kyc-add-owner']").Click();
        cut.FindAll("[data-testid='kyc-owner-row']").Should().HaveCount(1);

        cut.Find("[data-testid='kyc-owner-add-child']").Click();
        cut.FindAll("[data-testid='kyc-owner-row']").Should().HaveCount(2);

        // Removing the parent removes its subtree too.
        cut.FindAll("[data-testid='kyc-owner-remove']")[0].Click();
        cut.FindAll("[data-testid='kyc-owner-row']").Should().BeEmpty();
    }

    // ── Submission ───────────────────────────────────────────────────────────

    [Fact]
    public void Submit_OnReviewStep_SubmitsAndShowsTheSubmissionId()
    {
        var provider = new InMemoryKycProvider();
        KycSubmissionResult? submitted = null;
        var cut = Render(provider, p => p.Add(x => x.OnSubmitted, (KycSubmissionResult r) => submitted = r));

        // Person path without validator: Subject → Documents → Addresses → Declarations → Review.
        ClickNext(cut);
        ClickNext(cut);
        ClickNext(cut);
        ClickNext(cut);
        cut.Find("[data-testid='kyc-step-review']");

        cut.Find("[data-testid='kyc-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            submitted.Should().NotBeNull();
            submitted!.Success.Should().BeTrue();
            cut.Find("[data-testid='kyc-submitted']").TextContent.Should().Contain(submitted.SubmissionId!);
        });
        provider.Submissions.Should().HaveCount(1);
    }

    [Fact]
    public void Submit_WithValidatorAndInvalidDraft_BlocksAndShowsErrors()
    {
        var provider = new InMemoryKycProvider();
        var cut = Render(provider, p => p.Add(x => x.StepValidator, new KycStepFluentValidator(() => Today)));

        // Force-walk to review by filling nothing: without valid data Next is blocked,
        // so drive the wizard through the stepper is not possible either — instead
        // fill the subject, then jump forward with Next and assert the final gate.
        FillPersonSubject(cut);
        ClickNext(cut);            // → Documents (valid subject)
        cut.Find("[data-testid='kyc-add-document']").Click();
        cut.Find("[data-testid='kyc-doc-number']").Change("123");
        cut.Find("[data-testid='kyc-doc-issuer']").Change("City office");
        ClickNext(cut);            // → Addresses
        cut.Find("[data-testid='kyc-add-address']").Click();
        cut.Find("[data-testid='kyc-addr-street']").Change("Dlouhá 12");
        cut.Find("[data-testid='kyc-addr-city']").Change("Praha");
        cut.Find("[data-testid='kyc-addr-postal']").Change("110 00");
        cut.Find("[data-testid='kyc-addr-country']").Change("CZ");
        ClickNext(cut);            // → Declarations (left unanswered)

        // Declarations are invalid → Next must block the review transition.
        ClickNext(cut);

        cut.Find("[data-testid='kyc-step-declarations']");
        cut.Find("[data-testid='kyc-errors']").TextContent.Should().Contain("consent");
        provider.Submissions.Should().BeEmpty();
    }

    [Fact]
    public void Review_ShowsSummarySectionsForTheDraft()
    {
        var provider = new InMemoryKycProvider();
        provider.SaveDraftAsync(KycModelTests.PersonDraft("draft-42")).GetAwaiter().GetResult();
        var cut = Render(provider, p => p.Add(x => x.DraftId, "draft-42"));

        cut.WaitForAssertion(() => cut.Find("[data-testid='kyc-first-name']"));
        ClickNext(cut);
        ClickNext(cut);
        ClickNext(cut);
        ClickNext(cut);

        var review = cut.Find("[data-testid='kyc-step-review']");
        review.TextContent.Should().Contain("Bedřich");
        review.TextContent.Should().Contain("Dlouhá 12");
        cut.FindAll("[data-testid='kyc-review-section']").Should().HaveCountGreaterThan(2);
    }
}
