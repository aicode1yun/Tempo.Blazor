using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Tempo.Blazor.Components.DocumentEditor;

/// <summary>Small JS-module backed focus trap controller used by document editor modal surfaces.</summary>
internal sealed class DocumentEditorFocusTrapController(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private const string ModulePath = "./_content/Tempo.Blazor.DocumentEditor/js/document-editor/focus-management.mjs";
    private readonly IJSRuntime _jsRuntime = jsRuntime;
    private IJSObjectReference? _module;
    private ElementReference? _root;
    private bool _active;

    /// <summary>Activates the focus trap and moves initial focus inside the supplied root.</summary>
    public async ValueTask ActivateAsync(ElementReference root, ElementReference initialFocus)
    {
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
        _root = root;
        _active = true;
        await _module.InvokeVoidAsync("trapFocus", root, initialFocus);
    }

    /// <summary>Releases the active focus trap.</summary>
    public async ValueTask ReleaseAsync(bool restoreFocus = true)
    {
        if (!_active || _module is null || _root is null)
        {
            return;
        }

        try
        {
            await _module.InvokeVoidAsync("releaseFocusTrap", _root.Value, restoreFocus);
        }
        catch (JSDisconnectedException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        _active = false;
        _root = null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await ReleaseAsync(restoreFocus: false);
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }
}
