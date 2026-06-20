namespace Tempo.Blazor.Components.DocumentEditor.Registry;

/// <summary>Delegate-based <see cref="IDocumentEditorCommandEntry"/> for registering inline command adapters.</summary>
internal sealed class FuncDocumentEditorCommandEntry : IDocumentEditorCommandEntry
{
    private readonly Func<DocumentEditorCommandContext, bool> _computeEnabled;
    private readonly Func<DocumentEditorCommandContext, bool>? _computeVisible;
    private readonly Func<DocumentEditorCommandContext, string?>? _computeValue;
    private readonly Func<DocumentEditorCommandContext, object?, Task> _execute;

    public string Name { get; }
    public bool AffectsData { get; }
    public string? DescriptionKey { get; }
    public string? TooltipKey { get; }
    public string? Category { get; }
    public string? DefaultShortcut { get; }
    public string? Icon { get; }
    public string? DisabledReasonKey { get; }

    public FuncDocumentEditorCommandEntry(
        string name,
        bool affectsData,
        Func<DocumentEditorCommandContext, bool> computeEnabled,
        Func<DocumentEditorCommandContext, object?, Task> execute,
        Func<DocumentEditorCommandContext, string?>? computeValue = null,
        Func<DocumentEditorCommandContext, bool>? computeVisible = null,
        string? descriptionKey = null,
        string? tooltipKey = null,
        string? category = null,
        string? defaultShortcut = null,
        string? icon = null,
        string? disabledReasonKey = null)
    {
        Name = name;
        AffectsData = affectsData;
        _computeEnabled = computeEnabled;
        _computeVisible = computeVisible;
        _computeValue = computeValue;
        _execute = execute;
        DescriptionKey = descriptionKey;
        TooltipKey = tooltipKey;
        Category = category;
        DefaultShortcut = defaultShortcut;
        Icon = icon;
        DisabledReasonKey = disabledReasonKey;
    }

    public bool ComputeEnabled(DocumentEditorCommandContext context) => _computeEnabled(context);

    public bool ComputeVisible(DocumentEditorCommandContext context) => _computeVisible?.Invoke(context) ?? true;

    public string? ComputeValue(DocumentEditorCommandContext context) => _computeValue?.Invoke(context);

    public Task ExecuteAsync(DocumentEditorCommandContext context, object? payload = null) =>
        _execute(context, payload);
}
