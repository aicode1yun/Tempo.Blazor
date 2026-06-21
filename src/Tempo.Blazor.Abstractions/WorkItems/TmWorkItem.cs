using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Abstractions.WorkItems;

/// <summary>
/// Canonical, unified task / work-item model shared by every task-bearing component
/// (Gantt, Notion tasks, external work-item blocks, scheduler). A single
/// <see cref="ITmWorkItemProvider"/> supplies these so the same item can appear
/// consistently across components within one application.
/// </summary>
public class TmWorkItem
{
    // ── Identity ────────────────────────────────────────────────────────────

    /// <summary>Stable identifier of the work item.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Key of the source/provider this item belongs to (registry discriminator).</summary>
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>Provider-native identifier (e.g. DEMO-101, issue number) when sourced externally.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Absolute URL to the source item, when available.</summary>
    public string? Url { get; set; }

    // ── Content ─────────────────────────────────────────────────────────────

    /// <summary>Display title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Rich-text or plain description.</summary>
    public string? Description { get; set; }

    // ── Scheduling ──────────────────────────────────────────────────────────

    /// <summary>
    /// Planned start. Defaults to <see cref="DateTime.MinValue"/> for unscheduled sources
    /// (e.g. Notion tasks, which use <see cref="DueDate"/> instead).
    /// </summary>
    public DateTime Start { get; set; }

    /// <summary>
    /// Planned end. Defaults to <see cref="DateTime.MinValue"/> for unscheduled sources.
    /// </summary>
    public DateTime End { get; set; }

    /// <summary>Optional due date / deadline.</summary>
    public DateTime? DueDate { get; set; }

    /// <summary>When true, the item is rendered as a milestone (diamond).</summary>
    public bool IsMilestone { get; set; }

    /// <summary>Computed duration (<see cref="End"/> − <see cref="Start"/>).</summary>
    public TimeSpan Duration => End - Start;

    // ── Progress / status ───────────────────────────────────────────────────

    /// <summary>Completion percentage (0–100).</summary>
    public int PercentComplete { get; set; }

    /// <summary>Convenience completed flag (kept in sync by consumers; Done status implies completed).</summary>
    public bool IsCompleted { get; set; }

    /// <summary>Unified workflow status.</summary>
    public TmWorkItemStatus Status { get; set; } = TmWorkItemStatus.Open;

    /// <summary>Provider-native status label, when richer than the unified enum.</summary>
    public string? StatusLabel { get; set; }

    /// <summary>Status color token or sanitized CSS color supplied by the provider.</summary>
    public string? StatusColor { get; set; }

    /// <summary>Unified priority.</summary>
    public TmWorkItemPriority Priority { get; set; } = TmWorkItemPriority.Medium;

    /// <summary>Provider-native priority label, when richer than the unified enum.</summary>
    public string? PriorityLabel { get; set; }

    // ── Hierarchy ───────────────────────────────────────────────────────────

    /// <summary>Parent work item identifier. Null for root-level items.</summary>
    public string? ParentId { get; set; }

    /// <summary>Whether this item has a parent.</summary>
    public bool HasParent => !string.IsNullOrEmpty(ParentId);

    /// <summary>UI state: whether children are expanded (relevant for parent items).</summary>
    public bool IsExpanded { get; set; } = true;

    /// <summary>
    /// When false (default), a group bar spans min(Start)→max(End) of its direct children.
    /// Set to true to use this item's own Start/End regardless of children.
    /// </summary>
    public bool UseManualDates { get; set; }

    // ── People ──────────────────────────────────────────────────────────────

    /// <summary>People assigned to this item.</summary>
    public List<TmWorkItemAssignee> Assignees { get; set; } = [];

    // ── Origin (where an item was surfaced, e.g. a Notion page/block) ────────

    /// <summary>Source page identifier (e.g. Notion page), when applicable.</summary>
    public string? OriginPageId { get; set; }

    /// <summary>Source page title, when applicable.</summary>
    public string? OriginPageTitle { get; set; }

    /// <summary>Source block identifier, when applicable.</summary>
    public string? OriginBlockId { get; set; }

    // ── Type / presentation ─────────────────────────────────────────────────

    /// <summary>Provider-native item type label (e.g. Bug, Story).</summary>
    public string? TypeLabel { get; set; }

    /// <summary>Optional icon URL for the item type.</summary>
    public string? TypeIconUrl { get; set; }

    /// <summary>Bar/accent color as a CSS color string. Null uses the default.</summary>
    public string? Color { get; set; }

    // ── Effort / cost ───────────────────────────────────────────────────────

    /// <summary>Estimated effort in hours.</summary>
    public double? EstimationHours { get; set; }

    /// <summary>Actually logged hours.</summary>
    public double? LoggedHours { get; set; }

    /// <summary>Planned effort in hours (for budget tracking).</summary>
    public double? BudgetHours { get; set; }

    /// <summary>Actual monetary cost incurred so far.</summary>
    public decimal? ActualCost { get; set; }

    // ── Extensibility ───────────────────────────────────────────────────────

    /// <summary>Free-form tags / labels.</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>User-defined custom field values keyed by field id.</summary>
    public Dictionary<string, string?> CustomFields { get; set; } = [];

    /// <summary>Opaque provider-specific fields shown only by consumers that understand them.</summary>
    public Dictionary<string, string> Fields { get; set; } = [];

    /// <summary>Files attached to this item.</summary>
    public List<GanttAttachment> Attachments { get; set; } = [];

    /// <summary>Comments on this item.</summary>
    public List<GanttComment> Comments { get; set; } = [];

    /// <summary>Time log entries recorded against this item.</summary>
    public List<GanttTimeLogEntry> TimeLog { get; set; } = [];

    // ── Timestamps ──────────────────────────────────────────────────────────

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Last updated timestamp reported by the provider.</summary>
    public DateTime? UpdatedAt { get; set; }
}
