namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Rectangle used by signing geometry calculations.</summary>
/// <param name="X">Horizontal position.</param>
/// <param name="Y">Vertical position.</param>
/// <param name="Width">Rectangle width.</param>
/// <param name="Height">Rectangle height.</param>
public readonly record struct SigningRectangle(double X, double Y, double Width, double Height)
{
    /// <summary>Horizontal end position.</summary>
    public double Right => X + Width;

    /// <summary>Vertical end position.</summary>
    public double Bottom => Y + Height;
}
