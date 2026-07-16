using Bunit;
using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Compliance;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Kyc;

/// <summary>
/// bUnit tests for TmScreeningResultPanel: ordered rendering of screening findings
/// (category, severity, source, time, confidence), the confirm/dismiss resolution
/// workflow through IScreeningProvider, read-only mode and the empty state.
/// </summary>
public class TmScreeningResultPanelTests : LocalizationTestBase
{
    private static ScreeningFinding Finding(
        string id,
        ScreeningSeverity severity = ScreeningSeverity.Medium,
        ScreeningFindingStatus status = ScreeningFindingStatus.Pending,
        double confidence = 0.82,
        int dayOffset = 0,
        string category = "sanctions")
        => new()
        {
            Id = id,
            SubjectId = "subj-1",
            Category = category,
            Title = $"Match {id}",
            Description = $"Possible match for finding {id}",
            Source = "EU consolidated list",
            OccurredAt = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero).AddDays(dayOffset),
            Confidence = confidence,
            Severity = severity,
            Status = status
        };

    private IRenderedComponent<TmScreeningResultPanel> Render(
        IScreeningProvider provider,
        Action<Bunit.ComponentParameterCollectionBuilder<TmScreeningResultPanel>>? configure = null)
        => RenderComponent<TmScreeningResultPanel>(p =>
        {
            p.Add(x => x.Provider, provider);
            p.Add(x => x.SubjectId, "subj-1");
            configure?.Invoke(p);
        });

    // ── Rendering & ordering ─────────────────────────────────────────────────

    [Fact]
    public void RendersFindings_PendingFirst_ThenBySeverity_ThenNewest()
    {
        var provider = new InMemoryScreeningProvider(
        [
            Finding("resolved-critical", severity: ScreeningSeverity.Critical, status: ScreeningFindingStatus.Dismissed),
            Finding("pending-low", severity: ScreeningSeverity.Low),
            Finding("pending-high-old", severity: ScreeningSeverity.High, dayOffset: -3),
            Finding("pending-high-new", severity: ScreeningSeverity.High)
        ]);
        var cut = Render(provider);

        cut.WaitForAssertion(() =>
        {
            var ids = cut.FindAll("[data-testid='screening-finding']")
                .Select(e => e.GetAttribute("data-finding-id"))
                .ToList();
            ids.Should().ContainInOrder("pending-high-new", "pending-high-old", "pending-low", "resolved-critical");
        });
    }

    [Fact]
    public void Finding_ShowsCategorySeveritySourceTimeAndConfidence()
    {
        var provider = new InMemoryScreeningProvider([Finding("f1", severity: ScreeningSeverity.High, confidence: 0.9)]);
        var cut = Render(provider);

        cut.WaitForAssertion(() =>
        {
            var finding = cut.Find("[data-testid='screening-finding']");
            finding.TextContent.Should().Contain("sanctions");
            finding.TextContent.Should().Contain("EU consolidated list");
            finding.QuerySelector("[data-testid='screening-severity']")!
                .ClassList.Should().Contain("tm-screening__severity--high");
            finding.QuerySelector("[data-testid='screening-confidence']")!
                .GetAttribute("data-confidence").Should().Be("0.90");
        });
    }

    [Fact]
    public void Header_ShowsThePendingCount()
    {
        var provider = new InMemoryScreeningProvider(
        [
            Finding("f1"),
            Finding("f2"),
            Finding("f3", status: ScreeningFindingStatus.Confirmed)
        ]);
        var cut = Render(provider);

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='screening-pending-count']").TextContent.Should().Contain("2"));
    }

    [Fact]
    public void NoFindings_RendersTheEmptyState()
    {
        var cut = Render(new InMemoryScreeningProvider([]));

        cut.WaitForAssertion(() => cut.Find(".tm-empty-state"));
    }

    // ── Resolution workflow ──────────────────────────────────────────────────

    [Fact]
    public void Confirm_WithNote_ResolvesThroughTheProvider_AndRaisesTheEvent()
    {
        var provider = new InMemoryScreeningProvider([Finding("f1")]);
        ScreeningFinding? resolved = null;
        var cut = Render(provider, p =>
        {
            p.Add(x => x.CurrentUserName, "alice");
            p.Add(x => x.OnFindingResolved, (ScreeningFinding f) => resolved = f);
        });

        cut.WaitForElement("[data-testid='screening-confirm']").Click();
        cut.Find("[data-testid='screening-note']").Change("Verified against the register");
        cut.Find("[data-testid='screening-resolve-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            resolved.Should().NotBeNull();
            resolved!.Status.Should().Be(ScreeningFindingStatus.Confirmed);
            resolved.ResolutionNote.Should().Be("Verified against the register");
            resolved.ResolvedBy.Should().Be("alice");
            cut.Find("[data-testid='screening-status']")
                .ClassList.Should().Contain("tm-screening__status--confirmed");
        });
    }

    [Fact]
    public void Dismiss_MarksTheFindingDismissed()
    {
        var provider = new InMemoryScreeningProvider([Finding("f1")]);
        var cut = Render(provider);

        cut.WaitForElement("[data-testid='screening-dismiss']").Click();
        cut.Find("[data-testid='screening-resolve-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='screening-status']")
                .ClassList.Should().Contain("tm-screening__status--dismissed");
            cut.FindAll("[data-testid='screening-confirm']").Should().BeEmpty();
        });
    }

    [Fact]
    public void ResolutionForm_CanBeCancelled()
    {
        var provider = new InMemoryScreeningProvider([Finding("f1")]);
        var cut = Render(provider);

        cut.WaitForElement("[data-testid='screening-confirm']").Click();
        cut.Find("[data-testid='screening-resolve-cancel']").Click();

        cut.FindAll("[data-testid='screening-resolution-form']").Should().BeEmpty();
        cut.Find("[data-testid='screening-status']")
            .ClassList.Should().Contain("tm-screening__status--pending");
    }

    [Fact]
    public void ResolvedFinding_ShowsTheResolutionNoteAndResolver()
    {
        var finding = Finding("f1", status: ScreeningFindingStatus.Confirmed);
        finding.ResolutionNote = "True positive";
        finding.ResolvedBy = "bob";
        finding.ResolvedAt = new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero);
        var cut = Render(new InMemoryScreeningProvider([finding]));

        cut.WaitForAssertion(() =>
        {
            var resolution = cut.Find("[data-testid='screening-resolution']");
            resolution.TextContent.Should().Contain("True positive");
            resolution.TextContent.Should().Contain("bob");
        });
    }

    // ── Read-only mode ───────────────────────────────────────────────────────

    [Fact]
    public void ReadOnly_HidesTheResolutionActions()
    {
        var provider = new InMemoryScreeningProvider([Finding("f1")]);
        var cut = Render(provider, p => p.Add(x => x.ReadOnly, true));

        cut.WaitForAssertion(() => cut.Find("[data-testid='screening-finding']"));
        cut.FindAll("[data-testid='screening-confirm']").Should().BeEmpty();
        cut.FindAll("[data-testid='screening-dismiss']").Should().BeEmpty();
    }
}
