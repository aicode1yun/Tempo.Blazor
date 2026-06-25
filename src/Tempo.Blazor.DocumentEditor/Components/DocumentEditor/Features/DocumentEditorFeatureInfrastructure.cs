using Tempo.Blazor.Components.DocumentEditor.Registry;

namespace Tempo.Blazor.Components.DocumentEditor.Features;

/// <summary>Feature plug-in contract for the document editor.</summary>
public interface IDocumentEditorFeature
{
    /// <summary>Stable feature name used by host configuration and dependencies.</summary>
    string Name { get; }

    /// <summary>Feature names that must be bootstrapped before this feature.</summary>
    IReadOnlyList<string> Requires { get; }

    /// <summary>Registers feature-owned commands.</summary>
    void RegisterCommands(DocumentEditorCommandRegistry commands);

    /// <summary>Registers feature-owned toolbar items.</summary>
    void RegisterToolbar(DocumentEditorToolbarRegistry toolbar);

    /// <summary>Registers feature-owned keyboard shortcuts.</summary>
    void RegisterShortcuts(DocumentEditorShortcutRegistry shortcuts);

    /// <summary>Registers feature-owned floating UI surfaces.</summary>
    void RegisterFloatingUi(DocumentFloatingUiRegistry floatingUi);

    /// <summary>Configures schema rules owned by the feature.</summary>
    void ConfigureSchema(DocumentEditorSchemaBuilder schema);
}

/// <summary>Stable document editor feature names.</summary>
public static class DocumentEditorFeatureNames
{
    /// <summary>Inline text formatting feature.</summary>
    public const string TextFormatting = "textFormatting";

    /// <summary>Paragraph formatting and layout feature.</summary>
    public const string Paragraph = "paragraph";

    /// <summary>Clipboard import and paste cleanup feature.</summary>
    public const string Clipboard = "clipboard";

    /// <summary>Find and replace feature.</summary>
    public const string FindReplace = "findReplace";

    /// <summary>Image insertion and management feature.</summary>
    public const string Image = "image";

    /// <summary>Table insertion and editing feature.</summary>
    public const string Table = "table";

    /// <summary>Comments and comment review feature.</summary>
    public const string Comments = "comments";

    /// <summary>Track changes and revisions feature.</summary>
    public const string TrackChanges = "trackChanges";

    /// <summary>Headers and footers feature.</summary>
    public const string HeadersFooters = "headersFooters";

    /// <summary>External import and export feature.</summary>
    public const string ImportExport = "importExport";

    /// <summary>Protected document and editable regions feature.</summary>
    public const string RestrictedEditing = "restrictedEditing";

    /// <summary>Offline and collaborative editing feature.</summary>
    public const string OfflineCollaboration = "offlineCollaboration";
}

/// <summary>Feature bootstrap dependencies shared across registration phases.</summary>
public sealed record DocumentEditorFeatureBootstrapContext(
    DocumentEditorCommandRegistry Commands,
    DocumentEditorToolbarRegistry Toolbar,
    DocumentEditorShortcutRegistry Shortcuts,
    DocumentFloatingUiRegistry FloatingUi,
    DocumentEditorSchemaBuilder Schema);

/// <summary>Ordered registry for document editor features.</summary>
public sealed class DocumentEditorFeatureRegistry
{
    private readonly Dictionary<string, IDocumentEditorFeature> _features = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IDocumentEditorFeature> _registrationOrder = [];

    /// <summary>Registers a feature.</summary>
    public void Register(IDocumentEditorFeature feature)
    {
        ArgumentNullException.ThrowIfNull(feature);

        if (string.IsNullOrWhiteSpace(feature.Name))
        {
            throw new InvalidOperationException("Document editor feature name cannot be empty.");
        }

        if (_features.ContainsKey(feature.Name))
        {
            throw new InvalidOperationException($"Document editor feature '{feature.Name}' is already registered.");
        }

        _features.Add(feature.Name, feature);
        _registrationOrder.Add(feature);
    }

    /// <summary>Attempts to resolve a feature by name.</summary>
    public bool TryGet(string name, out IDocumentEditorFeature? feature) =>
        _features.TryGetValue(name, out feature);

    /// <summary>Resolves a feature by name or throws if it is not registered.</summary>
    public IDocumentEditorFeature GetRequired(string name) =>
        _features.TryGetValue(name, out var feature)
            ? feature
            : throw new InvalidOperationException($"Document editor feature '{name}' is not registered.");

    /// <summary>Gets all features in registration order.</summary>
    public IReadOnlyList<IDocumentEditorFeature> GetAll() => _registrationOrder;

    /// <summary>Gets registered features sorted so dependencies come first.</summary>
    public IReadOnlyList<IDocumentEditorFeature> GetOrderedFeatures()
    {
        var ordered = new List<IDocumentEditorFeature>();
        var states = new Dictionary<string, VisitState>(StringComparer.OrdinalIgnoreCase);

        foreach (var feature in _registrationOrder)
        {
            Visit(feature, ordered, states);
        }

        return ordered;
    }

    /// <summary>Runs all feature bootstrap phases in dependency order.</summary>
    public void Bootstrap(DocumentEditorFeatureBootstrapContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ordered = GetOrderedFeatures();
        foreach (var feature in ordered)
        {
            feature.ConfigureSchema(context.Schema);
        }

        foreach (var feature in ordered)
        {
            feature.RegisterCommands(context.Commands);
        }

        foreach (var feature in ordered)
        {
            feature.RegisterToolbar(context.Toolbar);
        }

        foreach (var feature in ordered)
        {
            feature.RegisterShortcuts(context.Shortcuts);
        }

        foreach (var feature in ordered)
        {
            feature.RegisterFloatingUi(context.FloatingUi);
        }
    }

    private void Visit(
        IDocumentEditorFeature feature,
        List<IDocumentEditorFeature> ordered,
        Dictionary<string, VisitState> states)
    {
        if (states.TryGetValue(feature.Name, out var state))
        {
            if (state == VisitState.Visiting)
            {
                throw new InvalidOperationException($"Document editor feature dependency cycle includes '{feature.Name}'.");
            }

            if (state == VisitState.Visited)
            {
                return;
            }
        }

        states[feature.Name] = VisitState.Visiting;

        foreach (var dependencyName in feature.Requires)
        {
            if (!_features.TryGetValue(dependencyName, out var dependency))
            {
                throw new InvalidOperationException(
                    $"Document editor feature '{feature.Name}' requires missing feature '{dependencyName}'.");
            }

            Visit(dependency, ordered, states);
        }

        states[feature.Name] = VisitState.Visited;
        ordered.Add(feature);
    }

    private enum VisitState
    {
        Visiting,
        Visited
    }
}

/// <summary>Registry for feature-owned keyboard shortcuts.</summary>
public sealed class DocumentEditorShortcutRegistry
{
    private readonly List<DocumentEditorShortcut> _shortcuts = [];

    /// <summary>Registers a keyboard shortcut for a command.</summary>
    public void Register(string commandName, string shortcut)
    {
        _shortcuts.Add(new DocumentEditorShortcut(commandName, shortcut));
    }

    /// <summary>Gets shortcuts in registration order.</summary>
    public IReadOnlyList<DocumentEditorShortcut> GetAll() => _shortcuts;
}

/// <summary>Keyboard shortcut definition owned by a feature.</summary>
public sealed record DocumentEditorShortcut(string CommandName, string Shortcut);

/// <summary>Registry for feature-owned floating UI surfaces.</summary>
public sealed class DocumentFloatingUiRegistry
{
    private readonly List<DocumentFloatingUiRegistration> _registrations = [];

    /// <summary>Registers a floating UI layer for a feature.</summary>
    public void Register(string layerId, string featureName)
    {
        _registrations.Add(new DocumentFloatingUiRegistration(layerId, featureName));
    }

    /// <summary>Gets floating UI registrations in registration order.</summary>
    public IReadOnlyList<DocumentFloatingUiRegistration> GetAll() => _registrations;
}

/// <summary>Floating UI layer definition owned by a feature.</summary>
public sealed record DocumentFloatingUiRegistration(string LayerId, string FeatureName);

/// <summary>Schema extension builder used by document editor features.</summary>
public sealed class DocumentEditorSchemaBuilder
{
    private readonly List<DocumentEditorSchemaRule> _rules = [];

    /// <summary>Registers a schema rule for a feature.</summary>
    public void RegisterRule(string featureName, string rule)
    {
        _rules.Add(new DocumentEditorSchemaRule(featureName, rule));
    }

    /// <summary>Gets schema rules in registration order.</summary>
    public IReadOnlyList<DocumentEditorSchemaRule> GetRules() => _rules;
}

/// <summary>Schema rule definition owned by a feature.</summary>
public sealed record DocumentEditorSchemaRule(string FeatureName, string Rule);
