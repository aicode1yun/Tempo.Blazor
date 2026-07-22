using System.Globalization;
using System.Reflection;
using Tempo.Reporting.Engine.Text;

namespace Tempo.Reporting.Engine.Tests.Text;

/// <summary>
/// Validates <see cref="BidiAlgorithm"/> against the official Unicode BidiCharacterTest
/// conformance vectors (UCD 16.0.0). Each vector supplies the input code points, the
/// requested paragraph direction, and the expected paragraph level, per-character resolved
/// levels (with 'x' for characters removed by rule X9) and left-to-right visual ordering.
/// </summary>
public sealed class BidiConformanceTests
{
    // Resource embedded from the Unicode Character Database 16.0.0 BidiCharacterTest.txt.
    // A representative subset is committed (see BidiCharacterTest.Subset.txt); set the
    // TEMPO_BIDI_FULL_TEST environment variable to a full BidiCharacterTest.txt path to
    // run every vector locally.
    private const string SubsetResource = "Tempo.Reporting.Engine.Tests.Text.BidiCharacterTest.Subset.txt";

    [Fact]
    public void MatchesUnicodeBidiCharacterTestVectors()
    {
        int passed = 0;
        int failed = 0;
        var failures = new List<string>();

        foreach ((string line, int lineNumber) in ReadVectorLines())
        {
            BidiVector vector = BidiVector.Parse(line);
            BidiAlgorithm.BidiCodePointResult result =
                BidiAlgorithm.ResolveCodePoints(vector.CodePoints, vector.ParagraphDirection);

            if (TryVerify(vector, result, out string? reason))
            {
                passed++;
            }
            else
            {
                failed++;
                if (failures.Count < 20)
                {
                    failures.Add($"line {lineNumber}: {reason}\n  input: {line}");
                }
            }
        }

        passed.Should().BeGreaterThan(0, "the conformance fixture must contain vectors");
        failed.Should().Be(0, $"all BidiCharacterTest vectors must pass. {failed} failed:\n" + string.Join("\n", failures));
    }

    private static bool TryVerify(BidiVector vector, BidiAlgorithm.BidiCodePointResult result, out string? reason)
    {
        if (result.ParagraphLevel != vector.ParagraphLevel)
        {
            reason = $"paragraph level {result.ParagraphLevel} != expected {vector.ParagraphLevel}";
            return false;
        }

        for (int i = 0; i < vector.ExpectedLevels.Length; i++)
        {
            int expected = vector.ExpectedLevels[i];
            if (expected < 0)
            {
                // 'x': the character must have been removed by rule X9.
                if (!result.Removed[i])
                {
                    reason = $"index {i} expected removed (x) but level {result.Levels[i]} was assigned";
                    return false;
                }
            }
            else
            {
                if (result.Removed[i])
                {
                    reason = $"index {i} unexpectedly removed; expected level {expected}";
                    return false;
                }

                if (result.Levels[i] != expected)
                {
                    reason = $"index {i} level {result.Levels[i]} != expected {expected}";
                    return false;
                }
            }
        }

        // Visual ordering: logical indices in left-to-right order, skipping X9-removed chars.
        var actualOrder = new List<int>();
        foreach (int logical in result.VisualToLogical)
        {
            if (!result.Removed[logical])
            {
                actualOrder.Add(logical);
            }
        }

        if (actualOrder.Count != vector.ExpectedOrder.Length)
        {
            reason = $"visual order length {actualOrder.Count} != expected {vector.ExpectedOrder.Length}";
            return false;
        }

        for (int i = 0; i < actualOrder.Count; i++)
        {
            if (actualOrder[i] != vector.ExpectedOrder[i])
            {
                reason = $"visual order[{i}] = {actualOrder[i]} != expected {vector.ExpectedOrder[i]}";
                return false;
            }
        }

        reason = null;
        return true;
    }

    private static IEnumerable<(string Line, int LineNumber)> ReadVectorLines()
    {
        string? fullPath = Environment.GetEnvironmentVariable("TEMPO_BIDI_FULL_TEST");
        if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
        {
            int number = 0;
            foreach (string raw in File.ReadLines(fullPath))
            {
                number++;
                if (IsVectorLine(raw))
                {
                    yield return (raw, number);
                }
            }

            yield break;
        }

        Assembly assembly = typeof(BidiConformanceTests).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(SubsetResource)
            ?? throw new InvalidOperationException($"Embedded resource '{SubsetResource}' not found.");
        using var reader = new StreamReader(stream);
        int lineNumber = 0;
        while (reader.ReadLine() is { } raw)
        {
            lineNumber++;
            if (IsVectorLine(raw))
            {
                yield return (raw, lineNumber);
            }
        }
    }

    private static bool IsVectorLine(string line)
        => line.Length > 0 && line[0] != '#' && !string.IsNullOrWhiteSpace(line);

    private sealed class BidiVector
    {
        public required int[] CodePoints { get; init; }

        public required sbyte? ParagraphDirection { get; init; }

        public required sbyte ParagraphLevel { get; init; }

        // -1 marks a character removed by X9 (an 'x' entry in the vector).
        public required int[] ExpectedLevels { get; init; }

        public required int[] ExpectedOrder { get; init; }

        public static BidiVector Parse(string line)
        {
            string[] fields = line.Split(';');
            int[] codePoints = ParseHex(fields[0]);
            sbyte? direction = fields[1].Trim() switch
            {
                "0" => (sbyte)0,
                "1" => (sbyte)1,
                _ => null, // 2 = auto
            };
            sbyte level = sbyte.Parse(fields[2].Trim(), CultureInfo.InvariantCulture);

            int[] levels = fields[3]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(static token => token == "x" ? -1 : int.Parse(token, CultureInfo.InvariantCulture))
                .ToArray();

            int[] order = fields[4]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(static token => int.Parse(token, CultureInfo.InvariantCulture))
                .ToArray();

            return new BidiVector
            {
                CodePoints = codePoints,
                ParagraphDirection = direction,
                ParagraphLevel = level,
                ExpectedLevels = levels,
                ExpectedOrder = order,
            };
        }

        private static int[] ParseHex(string field)
            => field
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(static token => int.Parse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
                .ToArray();
    }
}
