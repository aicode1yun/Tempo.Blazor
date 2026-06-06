using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Services;

/// <summary>Calculates swimlane cell positions and arranges child nodes.</summary>
public sealed class SwimlaneLayoutService
{
    /// <summary>Calculates which swimlane cell contains the given document point.</summary>
    public static (int Row, int Column)? ComputeCell(DiagramNode swimlane, double docX, double docY)
    {
        var data = swimlane.SwimlaneData;
        if (data is null) return null;

        double localX = docX - swimlane.X;
        double localY = docY - swimlane.Y;

        if (localX < 0 || localX > swimlane.W || localY < 0 || localY > swimlane.H)
            return null;

        if (data.IsHorizontal)
        {
            // Header is on the left, lanes are rows
            if (localX < data.HeaderSize) return null;
            localY = Math.Max(0, localY);
        }
        else
        {
            // Header is on the top, lanes are columns
            if (localY < data.HeaderSize) return null;
            localX = Math.Max(0, localX);
        }

        int row = ResolveIndex(localY, data.RowSizes, data.RowCount, swimlane.H - (data.IsHorizontal ? 0 : data.HeaderSize));
        int col = ResolveIndex(localX, data.ColumnSizes, data.ColumnCount, swimlane.W - (data.IsHorizontal ? data.HeaderSize : 0));

        if (row < 0 || row >= data.RowCount || col < 0 || col >= data.ColumnCount)
            return null;

        return (row, col);
    }

    /// <summary>Returns the absolute bounds of a specific swimlane cell.</summary>
    public static (double X, double Y, double W, double H) GetCellBounds(DiagramNode swimlane, int row, int column)
    {
        var data = swimlane.SwimlaneData ?? throw new InvalidOperationException("Node is not a swimlane.");

        double contentW = swimlane.W - (data.IsHorizontal ? data.HeaderSize : 0);
        double contentH = swimlane.H - (data.IsHorizontal ? 0 : data.HeaderSize);

        double yOffset = data.IsHorizontal ? 0 : data.HeaderSize;
        double xOffset = data.IsHorizontal ? data.HeaderSize : 0;

        double cellY = yOffset + GetCumulativeSize(row, data.RowSizes, data.RowCount, contentH);
        double cellH = GetSize(row, data.RowSizes, data.RowCount, contentH);

        double cellX = xOffset + GetCumulativeSize(column, data.ColumnSizes, data.ColumnCount, contentW);
        double cellW = GetSize(column, data.ColumnSizes, data.ColumnCount, contentW);

        return (swimlane.X + cellX, swimlane.Y + cellY, cellW, cellH);
    }

    /// <summary>Positions a child node inside the centre of its assigned swimlane cell.</summary>
    public static void ArrangeChild(DiagramNode swimlane, DiagramNode child)
    {
        if (child.SwimlaneRow < 0 || child.SwimlaneColumn < 0) return;

        var (cx, cy, cw, ch) = GetCellBounds(swimlane, child.SwimlaneRow, child.SwimlaneColumn);
        const double margin = 10;
        var minX = cx + margin;
        var minY = cy + margin;
        var maxX = Math.Max(minX, cx + cw - child.W - margin);
        var maxY = Math.Max(minY, cy + ch - child.H - margin);

        child.X = Clamp(child.X, minX, maxX);
        child.Y = Clamp(child.Y, minY, maxY);
    }

    /// <summary>Updates swimlane dimensions so that all rows and columns fit their defined sizes.</summary>
    public static void RecalculateSize(DiagramNode swimlane)
    {
        var data = swimlane.SwimlaneData;
        if (data is null) return;

        double contentW = swimlane.W - (data.IsHorizontal ? data.HeaderSize : 0);
        double contentH = swimlane.H - (data.IsHorizontal ? 0 : data.HeaderSize);

        double targetW = data.IsHorizontal ? data.HeaderSize : 0;
        double targetH = data.IsHorizontal ? 0 : data.HeaderSize;

        for (int c = 0; c < data.ColumnCount; c++)
            targetW += GetSize(c, data.ColumnSizes, data.ColumnCount, contentW);

        for (int r = 0; r < data.RowCount; r++)
            targetH += GetSize(r, data.RowSizes, data.RowCount, contentH);

        // Ensure minimum dimensions
        swimlane.W = Math.Max(swimlane.W, targetW);
        swimlane.H = Math.Max(swimlane.H, targetH);
    }

    /// <summary>Returns the default size for a newly added row or column.</summary>
    public static double GetDefaultLaneSize(DiagramNodeSwimlaneData data, bool forRow)
    {
        if (forRow && data.RowSizes.Count > 0)
            return data.RowSizes.Average();
        if (!forRow && data.ColumnSizes.Count > 0)
            return data.ColumnSizes.Average();
        return 80;
    }

    private static int ResolveIndex(double localPos, List<double> explicitSizes, int count, double totalSize)
    {
        if (explicitSizes.Count >= count)
        {
            double pos = 0;
            for (int i = 0; i < count; i++)
            {
                pos += explicitSizes[i];
                if (localPos < pos) return i;
            }
            return count - 1;
        }

        double defaultSize = count > 0 ? totalSize / count : totalSize;
        int idx = (int)(localPos / defaultSize);
        return Math.Clamp(idx, 0, count - 1);
    }

    private static double GetCumulativeSize(int index, List<double> explicitSizes, int count, double totalSize)
    {
        if (index <= 0) return 0;
        if (explicitSizes.Count >= count)
        {
            double sum = 0;
            for (int i = 0; i < index && i < explicitSizes.Count; i++)
                sum += explicitSizes[i];
            return sum;
        }
        double defaultSize = count > 0 ? totalSize / count : 0;
        return index * defaultSize;
    }

    private static double GetSize(int index, List<double> explicitSizes, int count, double totalSize)
    {
        if (explicitSizes.Count > index && index >= 0)
            return explicitSizes[index];
        return count > 0 ? totalSize / count : 0;
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
