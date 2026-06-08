using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.NotionEditor.UI;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class TmNotionAuditLogPanelTests : LocalizationTestBase
{
    public TmNotionAuditLogPanelTests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["Tm_Close"] = "Close",
            ["Tm_Loading"] = "Loading",
            ["Tm_Retry"] = "Retry",
            ["Notion_Audit_Title"] = "Audit log",
            ["Notion_Audit_Action_Create"] = "Created",
            ["Notion_Audit_Action_Edit"] = "Edited",
            ["Notion_Audit_Action_Delete"] = "Deleted",
            ["Notion_Audit_Action_Move"] = "Moved",
            ["Notion_Audit_Action_Restrict"] = "Restricted",
            ["Notion_Audit_Export"] = "Export CSV",
            ["Notion_Audit_Empty"] = "No audit entries match the current filters.",
            ["Notion_Audit_Filter"] = "Filter",
            ["Notion_Audit_User"] = "User",
            ["Notion_Audit_Action"] = "Action",
            ["Notion_Audit_Action_All"] = "All actions",
            ["Notion_Audit_From"] = "From",
            ["Notion_Audit_To"] = "To",
            ["Notion_Audit_Clear"] = "Clear",
            ["Notion_Audit_DownloadReady"] = "CSV ready",
            ["Notion_Audit_LoadError"] = "Audit log could not be loaded.",
            ["Notion_Audit_Timestamp"] = "Timestamp",
            ["Notion_Audit_Target"] = "Target",
            ["Notion_Audit_Details"] = "Details",
            ["Notion_Audit_Count"] = "{0} entries",
            ["Notion_Audit_Page"] = "Audit log page",
            ["Notion_Audit_PageStatus"] = "Page {0} of {1}",
            ["Notion_Audit_Previous"] = "Previous page",
            ["Notion_Audit_Next"] = "Next page"
        });
    }

    [Fact]
    public void PanelFiltersByUserActionAndDate()
    {
        var cut = RenderPanel(new FakeAuditProvider(SampleEntries()));

        cut.WaitForAssertion(() => cut.FindAll("[data-testid='notion-audit-entry']").Should().HaveCount(3));
        cut.Find("[data-testid='notion-audit-user-filter']").Input("Alice");
        cut.Find("[data-testid='notion-audit-action-filter']").Change("edit");
        cut.Find("[data-testid='notion-audit-from-filter']").Change("2026-01-12");
        cut.Find("[data-testid='notion-audit-to-filter']").Change("2026-01-12");
        cut.Find("[data-testid='notion-audit-apply']").Click();

        cut.WaitForAssertion(() =>
        {
            var entries = cut.FindAll("[data-testid='notion-audit-entry']");
            entries.Should().ContainSingle();
            entries[0].TextContent.Should().Contain("Alice Morgan");
            entries[0].TextContent.Should().Contain("Edited");
        });
    }

    [Fact]
    public void PanelExportsFilteredCsv()
    {
        var cut = RenderPanel(new FakeAuditProvider(SampleEntries()));

        cut.Find("[data-testid='notion-audit-user-filter']").Input("Bob");
        cut.Find("[data-testid='notion-audit-apply']").Click();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='notion-audit-entry']").Should().ContainSingle());
        cut.Find("[data-testid='notion-audit-export']").Click();

        cut.WaitForAssertion(() =>
        {
            var href = cut.Find("[data-testid='notion-audit-export-link']").GetAttribute("href");
            href.Should().StartWith("data:text/csv;charset=utf-8,");
            Uri.UnescapeDataString(href![(href!.IndexOf(',') + 1)..]).Should().Contain("Bob Stone").And.Contain("Created");
        });
    }

    [Fact]
    public void PanelRendersEmptyStateAndPagesManyEntries()
    {
        var entries = Enumerable.Range(0, 13)
            .Select(index => Entry(
                id: $"entry-{index:00}",
                timestamp: new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc).AddMinutes(-index),
                userId: "alice",
                user: "Alice Morgan",
                action: "edit",
                title: $"Paged {index:00}"))
            .ToArray();

        var cut = RenderPanel(new FakeAuditProvider(entries), pageSize: 5);

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='notion-audit-entry']").Should().HaveCount(5);
            cut.Find("[data-testid='notion-audit-page']").TextContent.Should().Contain("Page 1 of 3");
        });

        cut.Find("[data-testid='notion-audit-next']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='notion-audit-page']").TextContent.Should().Contain("Page 2 of 3"));

        cut.Find("[data-testid='notion-audit-user-filter']").Input("nobody");
        cut.Find("[data-testid='notion-audit-apply']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='notion-audit-empty']").TextContent.Should().Contain("No audit entries"));
    }

    private IRenderedFragment RenderPanel(INotionAuditProvider provider, int pageSize = 10)
        => Render(builder =>
        {
            builder.OpenComponent<TmNotionAuditLogPanel>(0);
            builder.AddAttribute(1, nameof(TmNotionAuditLogPanel.AuditProvider), provider);
            builder.AddAttribute(2, nameof(TmNotionAuditLogPanel.PageSize), pageSize);
            builder.CloseComponent();
        });

    private static IReadOnlyList<AuditEntryDto> SampleEntries()
        =>
        [
            Entry("audit-001", new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc), "bob", "Bob Stone", "create", "Created page"),
            Entry("audit-002", new DateTime(2026, 1, 12, 8, 0, 0, DateTimeKind.Utc), "alice", "Alice Morgan", "edit", "Edited page"),
            Entry("audit-003", new DateTime(2026, 1, 14, 8, 0, 0, DateTimeKind.Utc), "alice", "Alice Morgan", "restrict", "Restricted page")
        ];

    private static AuditEntryDto Entry(string id, DateTime timestamp, string userId, string user, string action, string title)
        => new()
        {
            Id = id,
            Timestamp = timestamp,
            UserId = userId,
            UserDisplayName = user,
            Action = action,
            TargetType = "page",
            TargetId = "11111111-1111-1111-1111-111111111111",
            Details = new Dictionary<string, string> { ["title"] = title }
        };

    private sealed class FakeAuditProvider(IReadOnlyList<AuditEntryDto> entries) : INotionAuditProvider
    {
        public Task LogAsync(AuditEntryDto entry, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PagedResult<AuditEntryDto>> GetEntriesAsync(AuditLogFilter filter, NotionAuditQuery paging, CancellationToken cancellationToken = default)
        {
            var matches = entries
                .Where(entry => string.IsNullOrWhiteSpace(filter.UserId)
                    || entry.UserId.Contains(filter.UserId, StringComparison.OrdinalIgnoreCase)
                    || entry.UserDisplayName.Contains(filter.UserId, StringComparison.OrdinalIgnoreCase))
                .Where(entry => string.IsNullOrWhiteSpace(filter.Action) || string.Equals(entry.Action, filter.Action, StringComparison.OrdinalIgnoreCase))
                .Where(entry => filter.From is null || DateOnly.FromDateTime(entry.Timestamp) >= filter.From.Value)
                .Where(entry => filter.To is null || DateOnly.FromDateTime(entry.Timestamp) <= filter.To.Value)
                .OrderByDescending(entry => entry.Timestamp)
                .ToArray();

            var take = Math.Clamp(paging.Take, 1, 100);
            return Task.FromResult(new PagedResult<AuditEntryDto>
            {
                Items = matches.Skip(Math.Max(0, paging.Skip)).Take(take).ToArray(),
                TotalCount = matches.Length,
                Page = Math.Max(0, paging.Skip) / take + 1,
                PageSize = take
            });
        }
    }
}
