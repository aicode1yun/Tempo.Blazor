using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Wireframe.Models;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.TempoBlocks;

public partial class TmNotionWireframeEditModal : ComponentBase
{
    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter, EditorRequired]
    public Guid WireframeDocumentId { get; set; }

    [Parameter]
    public IWireframeDocumentProvider? Provider { get; set; }

    [Parameter] public EventCallback<(WireframeDocument Document, string SvgPreview)> OnSaved     { get; set; }
    [Parameter] public EventCallback                                                    OnDiscarded { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private DynamicComponent? _editorRef;
    private WireframeDocument? _document;
    private bool                _loading = true;
    private bool                _saving;
    private static readonly Type? WireframeEditorComponentType = ResolveWireframeEditorComponentType();

    private Dictionary<string, object?> WireframeEditorParameters => new()
    {
        ["Document"] = _document,
        ["DocumentChanged"] = EventCallback.Factory.Create<WireframeDocument>(this, value => _document = value),
        ["ShowToolbox"] = true,
        ["ShowPropertiesPanel"] = true,
        ["ShowMinimap"] = true,
        ["ShowToolbar"] = true,
        ["ShowGrid"] = true,
        ["ShowPageTabs"] = true,
        ["Class"] = "tm-notion-wireframe-edit-modal__editor"
    };

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        if (Provider is not null)
        {
            try { _document = await Provider.GetWireframeDocumentAsync(WireframeDocumentId); }
            catch { }
        }
        if (_document is null)
        {
            _document = new WireframeDocument();
            _document.EnsureActivePage();
        }
        _loading = false;
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private async Task SaveAsync()
    {
        if (_document is null || _saving) return;
        _saving = true;
        string svg = string.Empty;
        try
        {
            svg = await TryExportSvgAsync();
        }
        catch { }
        finally
        {
            _saving = false;
        }
        await OnSaved.InvokeAsync((_document, svg));
    }

    private async Task DiscardAsync() => await OnDiscarded.InvokeAsync();

    private async Task<string> TryExportSvgAsync()
    {
        var instance = _editorRef?.Instance;
        var method = instance?.GetType().GetMethod("ExportSvgAsync");
        if (method?.Invoke(instance, null) is Task<string> task)
        {
            return await task;
        }

        return string.Empty;
    }

    private static Type? ResolveWireframeEditorComponentType()
        => Type.GetType("Tempo.Blazor.Components.Wireframe.TmWireframeEditor, Tempo.Blazor.Wireframe")
           ?? AppDomain.CurrentDomain.GetAssemblies()
               .Select(assembly => assembly.GetType("Tempo.Blazor.Components.Wireframe.TmWireframeEditor", throwOnError: false))
               .FirstOrDefault(type => type is not null);
}
