using System.Reflection;
using ModelContextProtocol.Server;
using Tempo.Blazor.Mcp;

namespace Tempo.Blazor.Mcp.Tests;

/// <summary>
/// Drift guard for docs/document-mcp-tools.md: every document MCP tool registered in
/// <see cref="TempoDocumentEditorMcp.ToolTypes"/> must have a section in the catalog, and the
/// catalog must not document tools that no longer exist. A new tool without documentation (or a
/// renamed tool with a stale section) fails the suite.
/// </summary>
public class DocumentMcpToolsDocumentationDriftTests
{
    private const string DocRelativePath = "docs/document-mcp-tools.md";

    private static IReadOnlyList<string> RegisteredToolNames()
        => TempoDocumentEditorMcp.ToolTypes
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    private static string DocText()
    {
        var path = Path.Combine(RepoRoot(), DocRelativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(path).Should().BeTrue($"the document MCP tool catalog must exist at {DocRelativePath}");
        return File.ReadAllText(path);
    }

    [Fact]
    public void EveryRegisteredTool_HasACatalogSection()
    {
        var doc = DocText();
        var missing = RegisteredToolNames()
            .Where(name => !doc.Contains($"### `{name}`", StringComparison.Ordinal))
            .ToList();

        missing.Should().BeEmpty(
            $"every document MCP tool needs a '### `<name>`' section in {DocRelativePath} — document the new/renamed tools");
    }

    [Fact]
    public void CatalogDoesNotDocumentUnknownTools()
    {
        var doc = DocText();
        var known = RegisteredToolNames().ToHashSet(StringComparer.Ordinal);
        var documented = System.Text.RegularExpressions.Regex
            .Matches(doc, @"^### `([a-z0-9_]+)`", System.Text.RegularExpressions.RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToList();

        documented.Should().NotBeEmpty("the catalog must use '### `<tool_name>`' sections");
        documented.Where(name => !known.Contains(name)).Should().BeEmpty(
            "the catalog documents tools that are no longer registered — remove or rename the stale sections");
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx")))
        {
            directory = directory.Parent!;
        }

        directory.Should().NotBeNull();
        return directory!.FullName;
    }
}
