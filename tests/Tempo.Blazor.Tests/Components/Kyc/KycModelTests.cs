using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Kyc;

/// <summary>
/// Model tests for the KYC/compliance stack: deep-cloning drafts, the in-memory
/// draft provider (persist + submit), and the in-memory screening provider with
/// clone-on-read semantics and finding resolution.
/// </summary>
public class KycModelTests
{
    internal static KycDraft PersonDraft(string id = "draft-1")
        => new()
        {
            Id = id,
            SubjectKind = KycSubjectKind.Person,
            Person = new KycPersonIdentity
            {
                FirstName = "Bedřich",
                LastName = "Novák",
                DateOfBirth = new DateOnly(1980, 5, 12),
                Nationality = "CZ"
            },
            Documents =
            [
                new KycDocument
                {
                    Id = "doc-1",
                    Kind = KycDocumentKind.NationalId,
                    Number = "123456789",
                    IssuedBy = "Magistrát Praha",
                    ValidUntil = new DateOnly(2032, 1, 1)
                }
            ],
            Addresses =
            [
                new KycAddress
                {
                    Id = "addr-1",
                    Kind = KycAddressKind.Permanent,
                    Street = "Dlouhá 12",
                    City = "Praha",
                    PostalCode = "110 00",
                    Country = "CZ"
                }
            ],
            Declarations = new KycDeclarations
            {
                IsPoliticallyExposed = false,
                SourceOfFunds = "Employment income",
                ConsentGiven = true
            }
        };

    internal static KycDraft CompanyDraft(string id = "draft-co")
    {
        var draft = PersonDraft(id);
        draft.SubjectKind = KycSubjectKind.Company;
        draft.Company = new KycCompanyIdentity
        {
            Name = "Řehoř a syn s.r.o.",
            RegistrationNumber = "12345678",
            LegalForm = "s.r.o.",
            Country = "CZ"
        };
        draft.OwnershipRoot = new KycOwnershipNode
        {
            Id = "own-root",
            Name = "Řehoř a syn s.r.o.",
            Children =
            [
                new KycOwnershipNode { Id = "own-1", Name = "Jan Řehoř", SharePercent = 60m, IsBeneficialOwner = true },
                new KycOwnershipNode
                {
                    Id = "own-2",
                    Name = "Holding a.s.",
                    SharePercent = 40m,
                    Children = [new KycOwnershipNode { Id = "own-2-1", Name = "Petr Král", SharePercent = 100m, IsBeneficialOwner = true }]
                }
            ]
        };
        return draft;
    }

    // ── KycDraft.Clone ───────────────────────────────────────────────────────

    [Fact]
    public void Clone_IsDeep_ForDocumentsAddressesAndOwnership()
    {
        var original = CompanyDraft();

        var clone = original.Clone();
        clone.Person.FirstName = "Changed";
        clone.Documents[0].Number = "999";
        clone.Addresses[0].City = "Brno";
        clone.OwnershipRoot.Children[0].SharePercent = 1m;
        clone.OwnershipRoot.Children[1].Children[0].Name = "Someone Else";
        clone.Declarations.ConsentGiven = false;

        original.Person.FirstName.Should().Be("Bedřich");
        original.Documents[0].Number.Should().Be("123456789");
        original.Addresses[0].City.Should().Be("Praha");
        original.OwnershipRoot.Children[0].SharePercent.Should().Be(60m);
        original.OwnershipRoot.Children[1].Children[0].Name.Should().Be("Petr Král");
        original.Declarations.ConsentGiven.Should().BeTrue();
    }

    // ── InMemoryKycProvider ──────────────────────────────────────────────────

    [Fact]
    public async Task SaveAndLoadDraft_RoundTrips()
    {
        var provider = new InMemoryKycProvider();

        await provider.SaveDraftAsync(PersonDraft());
        var loaded = await provider.LoadDraftAsync("draft-1");

        loaded.Should().NotBeNull();
        loaded!.Person.FirstName.Should().Be("Bedřich");
        loaded.Documents.Should().HaveCount(1);
    }

    [Fact]
    public async Task LoadDraft_ReturnsFreshInstances_NotTheStoredReference()
    {
        var provider = new InMemoryKycProvider();
        await provider.SaveDraftAsync(PersonDraft());

        var first = await provider.LoadDraftAsync("draft-1");
        first!.Person.FirstName = "Mutated";
        var second = await provider.LoadDraftAsync("draft-1");

        second!.Person.FirstName.Should().Be("Bedřich");
    }

    [Fact]
    public async Task SaveDraft_StoresACopy_LaterCallerMutationsAreInvisible()
    {
        var provider = new InMemoryKycProvider();
        var draft = PersonDraft();
        await provider.SaveDraftAsync(draft);

        draft.Person.LastName = "Mutated";

        (await provider.LoadDraftAsync("draft-1"))!.Person.LastName.Should().Be("Novák");
    }

    [Fact]
    public async Task LoadDraft_UnknownId_ReturnsNull()
    {
        var provider = new InMemoryKycProvider();

        (await provider.LoadDraftAsync("missing")).Should().BeNull();
    }

    [Fact]
    public async Task Submit_ReturnsSuccessWithSubmissionId_AndRecordsTheSubmission()
    {
        var provider = new InMemoryKycProvider();

        var result = await provider.SubmitAsync(PersonDraft());

        result.Success.Should().BeTrue();
        result.SubmissionId.Should().NotBeNullOrEmpty();
        provider.Submissions.Should().ContainSingle(d => d.Id == "draft-1");
    }

    [Fact]
    public async Task Submit_RemovesTheDraft_SoItIsNoLongerPending()
    {
        var provider = new InMemoryKycProvider();
        await provider.SaveDraftAsync(PersonDraft());

        await provider.SubmitAsync(PersonDraft());

        (await provider.LoadDraftAsync("draft-1")).Should().BeNull();
    }

    // ── InMemoryScreeningProvider ────────────────────────────────────────────

    private static ScreeningFinding Finding(
        string id,
        string subjectId = "subj-1",
        ScreeningSeverity severity = ScreeningSeverity.Medium,
        ScreeningFindingStatus status = ScreeningFindingStatus.Pending)
        => new()
        {
            Id = id,
            SubjectId = subjectId,
            Category = "sanctions",
            Title = $"Match {id}",
            Source = "EU list",
            OccurredAt = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero),
            Confidence = 0.82,
            Severity = severity,
            Status = status
        };

    [Fact]
    public async Task GetFindings_FiltersBySubject_AndClonesOnRead()
    {
        var provider = new InMemoryScreeningProvider([Finding("f1"), Finding("f2", subjectId: "other")]);

        var findings = await provider.GetFindingsAsync("subj-1");
        findings.Should().ContainSingle(f => f.Id == "f1");

        findings[0].Title = "Mutated";
        (await provider.GetFindingsAsync("subj-1"))[0].Title.Should().Be("Match f1");
    }

    [Fact]
    public async Task Resolve_Confirm_SetsStatusNoteResolverAndTimestamp()
    {
        var provider = new InMemoryScreeningProvider([Finding("f1")]);

        var resolved = await provider.ResolveAsync(new ScreeningResolutionRequest
        {
            FindingId = "f1",
            Status = ScreeningFindingStatus.Confirmed,
            Note = "Verified against the register",
            ResolvedBy = "alice"
        });

        resolved.Status.Should().Be(ScreeningFindingStatus.Confirmed);
        resolved.ResolutionNote.Should().Be("Verified against the register");
        resolved.ResolvedBy.Should().Be("alice");
        resolved.ResolvedAt.Should().NotBeNull();

        (await provider.GetFindingsAsync("subj-1"))[0].Status.Should().Be(ScreeningFindingStatus.Confirmed);
    }

    [Fact]
    public async Task Resolve_WithPendingStatus_Throws()
    {
        var provider = new InMemoryScreeningProvider([Finding("f1")]);

        var act = () => provider.ResolveAsync(new ScreeningResolutionRequest
        {
            FindingId = "f1",
            Status = ScreeningFindingStatus.Pending
        });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Resolve_UnknownFinding_Throws()
    {
        var provider = new InMemoryScreeningProvider([]);

        var act = () => provider.ResolveAsync(new ScreeningResolutionRequest
        {
            FindingId = "missing",
            Status = ScreeningFindingStatus.Dismissed
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
