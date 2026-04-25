using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Copies the style (Props) and optionally size of a single element to the static clipboard.</summary>
public sealed class CopyStyleCommand
{
    private readonly WireframeElement _element;
    private readonly bool _includeSize;

    public CopyStyleCommand(WireframeElement element, bool includeSize = false)
    {
        _element = element;
        _includeSize = includeSize;
    }

    public void Execute()
    {
        WireframeClipboard.StyleProps = WireframeClipboard.CloneProps(_element.Props);
        if (_includeSize)
        {
            WireframeClipboard.Width = _element.W;
            WireframeClipboard.Height = _element.H;
        }
    }
}
