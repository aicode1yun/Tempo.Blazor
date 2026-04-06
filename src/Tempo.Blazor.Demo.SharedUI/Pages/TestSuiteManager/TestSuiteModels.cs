using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Demo.SharedUI.Pages.TestSuiteManager;

// ── Tag ──────────────────────────────────────────────────────────────────────

public record TestTag(string Id, string Name, string Color) : ITag;

// ── Status constants ──────────────────────────────────────────────────────────

public static class TestStatus
{
    public const string Pass    = "Pass";
    public const string Fail    = "Fail";
    public const string Skipped = "Skipped";
    public const string NotRun  = "Not Run";

    public static string ColorFor(string status) => status switch
    {
        Pass    => "#22c55e",
        Fail    => "#ef4444",
        Skipped => "#f59e0b",
        _       => "#94a3b8",
    };
}

// ── TestSuite — ITreeNode<string> ─────────────────────────────────────────────

public class TestSuite : ITreeNode<string>
{
    public string Id        { get; init; }    = default!;
    public string Label     { get; init; }    = default!;
    public string? Icon     { get; init; }
    public bool IsLeaf      { get; set; }
    public bool IsLoading   { get; set; }
    public string? ParentId { get; set; }

    private readonly List<ITreeNode<string>> _children = [];
    public IReadOnlyList<ITreeNode<string>> Children => _children;

    public void AddChild(TestSuite child) => _children.Add(child);
    public void RemoveChild(TestSuite child) => _children.Remove(child);
}

// ── TestCase — IMultiViewListItem ─────────────────────────────────────────────

public class TestCase : IMultiViewListItem
{
    public string   Id           { get; init; } = default!;
    public string   Title        { get; init; } = default!;
    public string?  SubTitle     { get; init; }
    public string?  AvatarUrl    { get; init; }
    public string?  StatusLabel  { get; init; }
    public string?  StatusColor  { get; init; }
    public DateTimeOffset? Date  { get; init; }
    public IReadOnlyList<ITag>? Tags { get; init; }

    /// <summary>ID of the Test Suite this case belongs to.</summary>
    public string SuiteId { get; set; } = default!;
}
