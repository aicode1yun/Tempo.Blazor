using Bunit;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Modeling;
using Tempo.Blazor.Modeling;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Modeling;

public sealed class TmModelingIssuePanelTests : LocalizationTestBase
{
    [Fact]
    public void Renders_issue_count_and_items()
    {
        using var cut = RenderComponent<TmModelingIssuePanel>(parameters => parameters
            .Add(p => p.Issues, CreateIssues()));

        cut.Find("[data-testid='modeling-issue-panel']").GetAttribute("data-issue-count").Should().Be("3");
        cut.FindAll("button[data-testid^='modeling-issue-']").Should().HaveCount(3);
    }

    [Fact]
    public void Clicking_issue_emits_source_element_id()
    {
        ModelingIssueDto? selected = null;
        using var cut = RenderComponent<TmModelingIssuePanel>(parameters => parameters
            .Add(p => p.Issues, CreateIssues())
            .Add(p => p.OnIssueSelected, EventCallback.Factory.Create<ModelingIssueDto>(this, issue => selected = issue)));

        cut.Find("[data-testid='modeling-issue-1']").Click();

        selected.Should().NotBeNull();
        selected!.SourceElementId.Should().Be("task-review");
    }

    [Fact]
    public void Severity_classes_match_each_issue_type()
    {
        using var cut = RenderComponent<TmModelingIssuePanel>(parameters => parameters
            .Add(p => p.Issues, CreateIssues()));

        cut.Find("[data-severity='info']").ClassList.Should().Contain("tm-modeling-issue-panel__item--info");
        cut.Find("[data-severity='warning']").ClassList.Should().Contain("tm-modeling-issue-panel__item--warning");
        cut.Find("[data-severity='error']").ClassList.Should().Contain("tm-modeling-issue-panel__item--error");
        cut.Find(".tm-modeling-issue-panel__icon--info").Should().NotBeNull();
        cut.Find(".tm-modeling-issue-panel__icon--warning").Should().NotBeNull();
        cut.Find(".tm-modeling-issue-panel__icon--error").Should().NotBeNull();
    }

    [Fact]
    public void Empty_issues_render_positive_empty_state()
    {
        using var cut = RenderComponent<TmModelingIssuePanel>(parameters => parameters
            .Add(p => p.Issues, Array.Empty<ModelingIssueDto>()));

        cut.Find("[data-testid='modeling-issue-empty']").TextContent.Should().Contain("No issues found");
        cut.FindAll("[data-testid='modeling-issue-list']").Should().BeEmpty();
    }

    [Fact]
    public void Issue_without_source_is_ignored_on_click()
    {
        var selectedCount = 0;
        using var cut = RenderComponent<TmModelingIssuePanel>(parameters => parameters
            .Add(p => p.Issues, new[] { new ModelingIssueDto { Id = "orphan", Message = "No source" } })
            .Add(p => p.OnIssueSelected, EventCallback.Factory.Create<ModelingIssueDto>(this, _ => selectedCount++)));

        cut.Find("[data-testid='modeling-issue-0']").Click();

        selectedCount.Should().Be(0);
    }

    [Fact]
    public void Renders_large_issue_list_without_failing()
    {
        var issues = Enumerable.Range(0, 128)
            .Select(index => new ModelingIssueDto
            {
                Id = $"issue-{index}",
                Severity = ModelingIssueSeverity.Warning,
                SourceElementId = $"task-{index}",
                Message = $"Issue {index}"
            })
            .ToArray();

        using var cut = RenderComponent<TmModelingIssuePanel>(parameters => parameters
            .Add(p => p.Issues, issues));

        cut.Find("[data-testid='modeling-issue-panel']").GetAttribute("data-issue-count").Should().Be("128");
        cut.FindAll("button[data-testid^='modeling-issue-']").Should().HaveCount(128);
    }

    [Fact]
    public void Long_message_uses_wrapping_message_element()
    {
        var message = new string('A', 500);
        using var cut = RenderComponent<TmModelingIssuePanel>(parameters => parameters
            .Add(p => p.Issues, new[] { new ModelingIssueDto { Id = "long", SourceElementId = "task-long", Message = message } }));

        var messageElement = cut.Find(".tm-modeling-issue-panel__message");
        messageElement.TextContent.Should().Be(message);
        messageElement.ClassList.Should().Contain("tm-modeling-issue-panel__message");
    }

    private static ModelingIssueDto[] CreateIssues() =>
    [
        new()
        {
            Id = "info",
            Category = "Governance",
            Severity = ModelingIssueSeverity.Info,
            SourceElementId = "task-approve",
            Message = "Informational issue"
        },
        new()
        {
            Id = "warning",
            Category = "Validation",
            Severity = ModelingIssueSeverity.Warning,
            SourceElementId = "task-review",
            Message = "Warning issue",
            SuggestedFix = "Review the mapping."
        },
        new()
        {
            Id = "error",
            Category = "Generator",
            Severity = ModelingIssueSeverity.Error,
            SourceElementId = "task-ship",
            Message = "Error issue"
        }
    ];
}
