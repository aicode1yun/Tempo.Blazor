namespace Tempo.Blazor.Components.Wireframe.Stencil;

public sealed class StencilSize
{
    public double Width { get; init; }

    public double Height { get; init; }

    public StencilSize()
    {
    }

    public StencilSize(double width, double height)
    {
        Width = width;
        Height = height;
    }
}
