namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Helpers for normalized signing field geometry.</summary>
public static class SigningGeometryHelper
{
    /// <summary>Converts a normalized signing area to a pixel rectangle.</summary>
    /// <param name="area">Normalized area.</param>
    /// <param name="pageWidth">Rendered page width in pixels.</param>
    /// <param name="pageHeight">Rendered page height in pixels.</param>
    /// <returns>Pixel rectangle.</returns>
    public static SigningRectangle ToPixels(SigningFieldArea area, double pageWidth, double pageHeight)
    {
        ArgumentNullException.ThrowIfNull(area);

        return new SigningRectangle(
            area.X * pageWidth,
            area.Y * pageHeight,
            area.Width * pageWidth,
            area.Height * pageHeight);
    }

    /// <summary>Converts a pixel rectangle to a normalized signing area.</summary>
    /// <param name="rectangle">Pixel rectangle.</param>
    /// <param name="pageWidth">Rendered page width in pixels.</param>
    /// <param name="pageHeight">Rendered page height in pixels.</param>
    /// <returns>Normalized area.</returns>
    public static SigningFieldArea ToNormalized(SigningRectangle rectangle, double pageWidth, double pageHeight)
    {
        if (pageWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageWidth), "Page width must be greater than zero.");
        }

        if (pageHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageHeight), "Page height must be greater than zero.");
        }

        return new SigningFieldArea
        {
            X = rectangle.X / pageWidth,
            Y = rectangle.Y / pageHeight,
            Width = rectangle.Width / pageWidth,
            Height = rectangle.Height / pageHeight
        };
    }

    /// <summary>Moves an area by a normalized delta and clamps the result to the page.</summary>
    /// <param name="area">Area to move.</param>
    /// <param name="deltaX">Normalized horizontal delta.</param>
    /// <param name="deltaY">Normalized vertical delta.</param>
    /// <param name="minWidth">Minimum normalized width.</param>
    /// <param name="minHeight">Minimum normalized height.</param>
    /// <returns>Moved area copy.</returns>
    public static SigningFieldArea Move(
        SigningFieldArea area,
        double deltaX,
        double deltaY,
        double minWidth = 0,
        double minHeight = 0)
    {
        ArgumentNullException.ThrowIfNull(area);

        return Clamp(Copy(area, x: area.X + deltaX, y: area.Y + deltaY), minWidth, minHeight);
    }

    /// <summary>Resizes an area by a normalized delta and clamps the result to the page.</summary>
    /// <param name="area">Area to resize.</param>
    /// <param name="handle">Resize handle.</param>
    /// <param name="deltaX">Normalized horizontal delta.</param>
    /// <param name="deltaY">Normalized vertical delta.</param>
    /// <param name="minWidth">Minimum normalized width.</param>
    /// <param name="minHeight">Minimum normalized height.</param>
    /// <returns>Resized area copy.</returns>
    public static SigningFieldArea Resize(
        SigningFieldArea area,
        SigningResizeHandle handle,
        double deltaX,
        double deltaY,
        double minWidth = 0,
        double minHeight = 0)
    {
        ArgumentNullException.ThrowIfNull(area);

        var x = area.X;
        var y = area.Y;
        var width = area.Width;
        var height = area.Height;

        if (handle is SigningResizeHandle.NorthWest or SigningResizeHandle.West or SigningResizeHandle.SouthWest)
        {
            x += deltaX;
            width -= deltaX;
        }

        if (handle is SigningResizeHandle.NorthWest or SigningResizeHandle.North or SigningResizeHandle.NorthEast)
        {
            y += deltaY;
            height -= deltaY;
        }

        if (handle is SigningResizeHandle.NorthEast or SigningResizeHandle.East or SigningResizeHandle.SouthEast)
        {
            width += deltaX;
        }

        if (handle is SigningResizeHandle.SouthWest or SigningResizeHandle.South or SigningResizeHandle.SouthEast)
        {
            height += deltaY;
        }

        if (width < minWidth)
        {
            if (handle is SigningResizeHandle.NorthWest or SigningResizeHandle.West or SigningResizeHandle.SouthWest)
            {
                x -= minWidth - width;
            }

            width = minWidth;
        }

        if (height < minHeight)
        {
            if (handle is SigningResizeHandle.NorthWest or SigningResizeHandle.North or SigningResizeHandle.NorthEast)
            {
                y -= minHeight - height;
            }

            height = minHeight;
        }

        var resizesFromLeft = handle is SigningResizeHandle.NorthWest or SigningResizeHandle.West or SigningResizeHandle.SouthWest;
        var resizesFromTop = handle is SigningResizeHandle.NorthWest or SigningResizeHandle.North or SigningResizeHandle.NorthEast;
        var resizesFromRight = handle is SigningResizeHandle.NorthEast or SigningResizeHandle.East or SigningResizeHandle.SouthEast;
        var resizesFromBottom = handle is SigningResizeHandle.SouthWest or SigningResizeHandle.South or SigningResizeHandle.SouthEast;

        if (resizesFromRight && !resizesFromLeft)
        {
            x = ClampValue(x, 0, 1 - minWidth);
            width = ClampValue(width, minWidth, 1 - x);
        }
        else if (resizesFromLeft)
        {
            var right = area.X + area.Width;
            x = ClampValue(x, 0, right - minWidth);
            width = ClampValue(right - x, minWidth, 1 - x);
        }

        if (resizesFromBottom && !resizesFromTop)
        {
            y = ClampValue(y, 0, 1 - minHeight);
            height = ClampValue(height, minHeight, 1 - y);
        }
        else if (resizesFromTop)
        {
            var bottom = area.Y + area.Height;
            y = ClampValue(y, 0, bottom - minHeight);
            height = ClampValue(bottom - y, minHeight, 1 - y);
        }

        return Copy(area, x, y, width, height);
    }

    /// <summary>Clamps an area to the 0..1 page coordinate space.</summary>
    /// <param name="area">Area to clamp.</param>
    /// <param name="minWidth">Minimum normalized width.</param>
    /// <param name="minHeight">Minimum normalized height.</param>
    /// <returns>Clamped area copy.</returns>
    public static SigningFieldArea Clamp(SigningFieldArea area, double minWidth = 0, double minHeight = 0)
    {
        ArgumentNullException.ThrowIfNull(area);

        var width = ClampValue(area.Width, minWidth, 1);
        var height = ClampValue(area.Height, minHeight, 1);
        var x = ClampValue(area.X, 0, 1 - width);
        var y = ClampValue(area.Y, 0, 1 - height);

        return Copy(area, x, y, width, height);
    }

    /// <summary>Returns a normalized bounding rectangle for the provided areas.</summary>
    /// <param name="areas">Areas to include.</param>
    /// <returns>Bounding rectangle or an empty rectangle when no areas are provided.</returns>
    public static SigningRectangle GetSelectionRectangle(IEnumerable<SigningFieldArea> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);

        var list = areas.ToList();
        if (list.Count == 0)
        {
            return new SigningRectangle(0, 0, 0, 0);
        }

        var x = list.Min(a => a.X);
        var y = list.Min(a => a.Y);
        var right = list.Max(a => a.X + a.Width);
        var bottom = list.Max(a => a.Y + a.Height);

        return new SigningRectangle(x, y, right - x, bottom - y);
    }

    private static double ClampValue(double value, double min, double max)
    {
        if (max < min)
        {
            max = min;
        }

        return Math.Min(Math.Max(value, min), max);
    }

    private static SigningFieldArea Copy(
        SigningFieldArea area,
        double? x = null,
        double? y = null,
        double? width = null,
        double? height = null)
    {
        return new SigningFieldArea
        {
            Uuid = area.Uuid,
            AttachmentUuid = area.AttachmentUuid,
            Page = area.Page,
            X = x ?? area.X,
            Y = y ?? area.Y,
            Width = width ?? area.Width,
            Height = height ?? area.Height,
            CellWidth = area.CellWidth,
            OptionUuid = area.OptionUuid
        };
    }
}
