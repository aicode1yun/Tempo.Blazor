using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Components.Files;

/// <summary>
/// Computes a line-by-line diff (via longest-common-subsequence) between two text versions.
/// Useful for <see cref="Abstractions.Interfaces.IFileVersioningHook"/> implementations that
/// version text content.
/// </summary>
public static class TmTextLineDiff
{
    /// <summary>Produces added/removed/unchanged lines describing the change from old to new text.</summary>
    public static IReadOnlyList<TmFileVersionDiffLine> Compute(string? oldText, string? newText)
    {
        var a = Split(oldText);
        var b = Split(newText);
        int n = a.Length, m = b.Length;

        // lcs[i, j] = length of the LCS of a[i..] and b[j..].
        var lcs = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
        {
            for (var j = m - 1; j >= 0; j--)
            {
                lcs[i, j] = a[i] == b[j]
                    ? lcs[i + 1, j + 1] + 1
                    : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            }
        }

        var result = new List<TmFileVersionDiffLine>();
        int x = 0, y = 0, oldLine = 1, newLine = 1;
        while (x < n && y < m)
        {
            if (a[x] == b[y])
            {
                result.Add(new TmFileVersionDiffLine { Kind = TmFileVersionDiffKind.Unchanged, Text = a[x], OldLineNumber = oldLine++, NewLineNumber = newLine++ });
                x++; y++;
            }
            else if (lcs[x + 1, y] >= lcs[x, y + 1])
            {
                result.Add(new TmFileVersionDiffLine { Kind = TmFileVersionDiffKind.Removed, Text = a[x], OldLineNumber = oldLine++ });
                x++;
            }
            else
            {
                result.Add(new TmFileVersionDiffLine { Kind = TmFileVersionDiffKind.Added, Text = b[y], NewLineNumber = newLine++ });
                y++;
            }
        }
        while (x < n) result.Add(new TmFileVersionDiffLine { Kind = TmFileVersionDiffKind.Removed, Text = a[x++], OldLineNumber = oldLine++ });
        while (y < m) result.Add(new TmFileVersionDiffLine { Kind = TmFileVersionDiffKind.Added, Text = b[y++], NewLineNumber = newLine++ });

        return result;
    }

    private static string[] Split(string? text)
        => string.IsNullOrEmpty(text) ? [] : text.Replace("\r\n", "\n").Split('\n');
}
