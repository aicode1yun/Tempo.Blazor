namespace Tempo.Reporting.Engine.Text;

/// <summary>Unicode Bidi_Class values (UAX#9). Numeric values are internal to the algorithm.</summary>
internal enum BidiClass : byte
{
    /// <summary>Left-to-Right.</summary>
    L = 0,

    /// <summary>Left-to-Right Embedding.</summary>
    LRE = 1,

    /// <summary>Left-to-Right Override.</summary>
    LRO = 2,

    /// <summary>Right-to-Left.</summary>
    R = 3,

    /// <summary>Right-to-Left Arabic.</summary>
    AL = 4,

    /// <summary>Right-to-Left Embedding.</summary>
    RLE = 5,

    /// <summary>Right-to-Left Override.</summary>
    RLO = 6,

    /// <summary>Pop Directional Format.</summary>
    PDF = 7,

    /// <summary>European Number.</summary>
    EN = 8,

    /// <summary>European Number Separator.</summary>
    ES = 9,

    /// <summary>European Number Terminator.</summary>
    ET = 10,

    /// <summary>Arabic Number.</summary>
    AN = 11,

    /// <summary>Common Number Separator.</summary>
    CS = 12,

    /// <summary>Non-Spacing Mark.</summary>
    NSM = 13,

    /// <summary>Boundary Neutral.</summary>
    BN = 14,

    /// <summary>Paragraph Separator.</summary>
    B = 15,

    /// <summary>Segment Separator.</summary>
    S = 16,

    /// <summary>Whitespace.</summary>
    WS = 17,

    /// <summary>Other Neutrals.</summary>
    ON = 18,

    /// <summary>Left-to-Right Isolate.</summary>
    LRI = 19,

    /// <summary>Right-to-Left Isolate.</summary>
    RLI = 20,

    /// <summary>First-Strong Isolate.</summary>
    FSI = 21,

    /// <summary>Pop Directional Isolate.</summary>
    PDI = 22,
}

/// <summary>
/// Result of running the Unicode Bidirectional Algorithm over a run of UTF-16 text.
/// All arrays are indexed by UTF-16 code-unit position within the input span; both
/// halves of a surrogate pair carry the values of their combined Unicode scalar value.
/// </summary>
/// <param name="ParagraphLevel">The resolved paragraph embedding level (0 = LTR, 1 = RTL).</param>
/// <param name="Levels">Resolved embedding level of each code unit (after rules through L1).</param>
/// <param name="VisualOrder">
/// Logical-to-visual map: <c>VisualOrder[logicalIndex]</c> is the left-to-right visual
/// position of the code unit (rule L2). The two code units of a surrogate pair keep their
/// logical order and occupy adjacent visual positions.
/// </param>
/// <param name="Mirrored">
/// Whether each code unit's glyph must be mirrored when drawn (rule L4): the character has
/// Bidi_Mirrored=Yes and a resolved odd (right-to-left) level.
/// </param>
public sealed record BidiResult(
    sbyte ParagraphLevel,
    IReadOnlyList<byte> Levels,
    IReadOnlyList<int> VisualOrder,
    IReadOnlyList<bool> Mirrored);

/// <summary>
/// Self-contained implementation of the Unicode Bidirectional Algorithm (UAX#9), covering
/// rules P2/P3, X1–X10 (explicit levels, directional overrides and isolates), W1–W7,
/// N0 (paired brackets), N1–N2, I1–I2, and the line-based rules L1/L2. Character properties
/// come from the Unicode Character Database 16.0.0 (see <see cref="BidiCharacterData"/>).
/// </summary>
public static class BidiAlgorithm
{
    /// <summary>
    /// Runs the Bidirectional Algorithm over a paragraph of text, treated as a single line.
    /// </summary>
    /// <param name="text">The paragraph text in UTF-16.</param>
    /// <param name="paragraphLevel">
    /// The explicit paragraph embedding level: <c>0</c> for LTR, <c>1</c> for RTL, or
    /// <c>null</c> to auto-detect via rules P2/P3.
    /// </param>
    /// <returns>The resolved levels, logical-to-visual reordering, and mirroring flags.</returns>
    public static BidiResult Resolve(ReadOnlySpan<char> text, sbyte? paragraphLevel = null)
    {
        if (paragraphLevel is { } level && level is not (0 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(paragraphLevel), level, "Paragraph level must be 0 (LTR), 1 (RTL) or null (auto).");
        }

        int charLength = text.Length;

        // Decode UTF-16 into Unicode scalar values, remembering the starting code unit of each.
        int[] codePoints = new int[charLength];
        int[] charStart = new int[charLength];
        int[] charSpan = new int[charLength]; // number of code units this scalar occupies (1 or 2)
        int cpCount = 0;
        for (int i = 0; i < charLength;)
        {
            char c = text[i];
            int cp;
            int span;
            if (char.IsHighSurrogate(c) && i + 1 < charLength && char.IsLowSurrogate(text[i + 1]))
            {
                cp = char.ConvertToUtf32(c, text[i + 1]);
                span = 2;
            }
            else
            {
                cp = c; // includes unpaired surrogates, treated as their own scalar
                span = 1;
            }

            codePoints[cpCount] = cp;
            charStart[cpCount] = i;
            charSpan[cpCount] = span;
            cpCount++;
            i += span;
        }

        Array.Resize(ref codePoints, cpCount);
        Array.Resize(ref charStart, cpCount);
        Array.Resize(ref charSpan, cpCount);

        BidiCodePointResult core = ResolveCodePoints(codePoints, paragraphLevel);

        // Expand code-point results back to UTF-16 code units.
        byte[] levels = new byte[charLength];
        bool[] mirrored = new bool[charLength];
        int[] visualOrder = new int[charLength];

        for (int cp = 0; cp < cpCount; cp++)
        {
            int start = charStart[cp];
            int span = charSpan[cp];
            byte lvl = core.Levels[cp];
            bool mir = !core.Removed[cp]
                       && (lvl & 1) == 1
                       && BidiCharacterData.IsMirrored(codePoints[cp]);
            for (int k = 0; k < span; k++)
            {
                levels[start + k] = lvl;
                mirrored[start + k] = mir;
            }
        }

        // Build the logical-to-visual map at code-unit granularity. Visiting code points
        // in visual order and emitting each one's code units in logical order keeps the two
        // halves of a surrogate pair together and yields a valid permutation.
        int visualPos = 0;
        for (int v = 0; v < cpCount; v++)
        {
            int cp = core.VisualToLogical[v];
            int start = charStart[cp];
            int span = charSpan[cp];
            for (int k = 0; k < span; k++)
            {
                visualOrder[start + k] = visualPos++;
            }
        }

        return new BidiResult(core.ParagraphLevel, levels, visualOrder, mirrored);
    }

    /// <summary>
    /// Returns the Bidi_Mirroring_Glyph for a code point, or the code point itself when there
    /// is no explicit mirror glyph (the renderer mirrors such glyphs geometrically). Rule L4.
    /// </summary>
    public static int GetMirrorGlyph(int codePoint) => BidiCharacterData.GetMirrorGlyph(codePoint);

    /// <summary>Returns whether a code point has the Bidi_Mirrored=Yes property.</summary>
    public static bool IsMirrored(int codePoint) => BidiCharacterData.IsMirrored(codePoint);

    /// <summary>Code-point-granular result used internally and by conformance tests.</summary>
    internal sealed record BidiCodePointResult(
        sbyte ParagraphLevel,
        byte[] Levels,
        bool[] Removed,
        int[] VisualToLogical);

    /// <summary>
    /// Runs the algorithm on an array of Unicode scalar values, resolving Bidi_Class and
    /// paired-bracket properties from the UCD. Exposed for direct conformance testing against
    /// the Unicode BidiCharacterTest vectors.
    /// </summary>
    internal static BidiCodePointResult ResolveCodePoints(ReadOnlySpan<int> codePoints, sbyte? paragraphLevel)
    {
        int length = codePoints.Length;
        byte[] types = new byte[length];
        byte[] pairTypes = new byte[length];
        int[] pairValues = new int[length];
        for (int i = 0; i < length; i++)
        {
            types[i] = (byte)BidiCharacterData.GetBidiClass(codePoints[i]);
            if (BidiCharacterData.TryGetBracket(codePoints[i], out byte pt, out int pv))
            {
                pairTypes[i] = pt;
                pairValues[i] = pv;
            }
        }

        var paragraph = new BidiParagraph(types, pairTypes, pairValues, paragraphLevel);
        return paragraph.Run();
    }

    /// <summary>
    /// Faithful port of the Unicode reference implementation of UAX#9 (BidiReference /
    /// BidiPBAReference), operating on Bidi_Class codes for a single paragraph.
    /// </summary>
    private sealed class BidiParagraph
    {
        private const byte L = (byte)BidiClass.L;
        private const byte LRE = (byte)BidiClass.LRE;
        private const byte LRO = (byte)BidiClass.LRO;
        private const byte R = (byte)BidiClass.R;
        private const byte AL = (byte)BidiClass.AL;
        private const byte RLE = (byte)BidiClass.RLE;
        private const byte RLO = (byte)BidiClass.RLO;
        private const byte PDF = (byte)BidiClass.PDF;
        private const byte EN = (byte)BidiClass.EN;
        private const byte ES = (byte)BidiClass.ES;
        private const byte ET = (byte)BidiClass.ET;
        private const byte AN = (byte)BidiClass.AN;
        private const byte CS = (byte)BidiClass.CS;
        private const byte NSM = (byte)BidiClass.NSM;
        private const byte BN = (byte)BidiClass.BN;
        private const byte B = (byte)BidiClass.B;
        private const byte S = (byte)BidiClass.S;
        private const byte WS = (byte)BidiClass.WS;
        private const byte ON = (byte)BidiClass.ON;
        private const byte LRI = (byte)BidiClass.LRI;
        private const byte RLI = (byte)BidiClass.RLI;
        private const byte FSI = (byte)BidiClass.FSI;
        private const byte PDI = (byte)BidiClass.PDI;

        private const int MaxDepth = 125;
        private const byte ImplicitLevel = 2;

        private readonly byte[] _initialTypes;
        private readonly byte[] _pairTypes;
        private readonly int[] _pairValues;
        private readonly int _textLength;

        private byte _paragraphLevel;
        private byte[] _resultTypes;
        private byte[] _resultLevels = Array.Empty<byte>();
        private int[] _matchingPdi = Array.Empty<int>();
        private int[] _matchingIsolateInitiator = Array.Empty<int>();

        public BidiParagraph(byte[] types, byte[] pairTypes, int[] pairValues, sbyte? paragraphLevel)
        {
            _initialTypes = types;
            _pairTypes = pairTypes;
            _pairValues = pairValues;
            _textLength = types.Length;
            _resultTypes = (byte[])types.Clone();
            _paragraphLevel = paragraphLevel switch
            {
                0 => 0,
                1 => 1,
                _ => ImplicitLevel,
            };
        }

        public BidiCodePointResult Run()
        {
            if (_textLength == 0)
            {
                sbyte level = _paragraphLevel == ImplicitLevel ? (sbyte)0 : (sbyte)_paragraphLevel;
                return new BidiCodePointResult(level, Array.Empty<byte>(), Array.Empty<bool>(), Array.Empty<int>());
            }

            // Rule P1 is presumed: the input is a single paragraph.
            DetermineMatchingIsolates();

            // Rules P2, P3.
            if (_paragraphLevel == ImplicitLevel)
            {
                _paragraphLevel = DetermineParagraphEmbeddingLevel(0, _textLength);
            }

            _resultLevels = new byte[_textLength];
            SetLevels(_resultLevels, 0, _textLength, _paragraphLevel);

            // Rules X1-X8.
            DetermineExplicitEmbeddingLevels();

            // Rule X9 is realised implicitly: removed characters are simply not copied into
            // isolating run sequences. Rule X10 + W/N/I run per isolating run sequence.
            foreach (IsolatingRunSequence sequence in DetermineIsolatingRunSequences())
            {
                sequence.ResolveWeakTypes();
                sequence.ResolvePairedBrackets();
                sequence.ResolveNeutralTypes();
                sequence.ResolveImplicitLevels();
                sequence.ApplyLevelsAndTypes();
            }

            AssignLevelsToCharactersRemovedByX9();

            // Rules L1 and L2 for the paragraph treated as a single line.
            byte[] lineLevels = ComputeLineLevels();
            int[] visualToLogical = ComputeReordering(lineLevels);

            bool[] removed = new bool[_textLength];
            for (int i = 0; i < _textLength; i++)
            {
                removed[i] = IsRemovedByX9(_initialTypes[i]);
            }

            return new BidiCodePointResult((sbyte)_paragraphLevel, lineLevels, removed, visualToLogical);
        }

        // Definition BD9.
        private void DetermineMatchingIsolates()
        {
            _matchingPdi = new int[_textLength];
            _matchingIsolateInitiator = new int[_textLength];

            for (int i = 0; i < _textLength; i++)
            {
                _matchingIsolateInitiator[i] = -1;
            }

            for (int i = 0; i < _textLength; i++)
            {
                _matchingPdi[i] = -1;

                byte t = _resultTypes[i];
                if (t is LRI or RLI or FSI)
                {
                    int depthCounter = 1;
                    for (int j = i + 1; j < _textLength; j++)
                    {
                        byte u = _resultTypes[j];
                        if (u is LRI or RLI or FSI)
                        {
                            depthCounter++;
                        }
                        else if (u == PDI)
                        {
                            depthCounter--;
                            if (depthCounter == 0)
                            {
                                _matchingPdi[i] = j;
                                _matchingIsolateInitiator[j] = i;
                                break;
                            }
                        }
                    }

                    if (_matchingPdi[i] == -1)
                    {
                        _matchingPdi[i] = _textLength;
                    }
                }
            }
        }

        // Rules P2, P3 (also used by X5c to resolve FSI).
        private byte DetermineParagraphEmbeddingLevel(int startIndex, int endIndex)
        {
            byte strongType = 0xFF; // unknown
            for (int i = startIndex; i < endIndex; i++)
            {
                byte t = _resultTypes[i];
                if (t is L or AL or R)
                {
                    strongType = t;
                    break;
                }

                if (t is FSI or LRI or RLI)
                {
                    i = _matchingPdi[i]; // skip to the matching PDI
                }
            }

            if (strongType == 0xFF || strongType == L)
            {
                return 0;
            }

            return 1; // AL, R
        }

        // Rules X1-X8.
        private void DetermineExplicitEmbeddingLevels()
        {
            var stack = new DirectionalStatusStack();
            stack.Push(_paragraphLevel, ON, false);
            int overflowIsolateCount = 0;
            int overflowEmbeddingCount = 0;
            int validIsolateCount = 0;

            for (int i = 0; i < _textLength; i++)
            {
                byte t = _resultTypes[i];
                switch (t)
                {
                    case RLE:
                    case LRE:
                    case RLO:
                    case LRO:
                    case RLI:
                    case LRI:
                    case FSI:
                        bool isIsolate = t is RLI or LRI or FSI;
                        bool isRtl = t is RLE or RLO or RLI;
                        if (t == FSI)
                        {
                            isRtl = DetermineParagraphEmbeddingLevel(i + 1, _matchingPdi[i]) == 1;
                        }

                        if (isIsolate)
                        {
                            _resultLevels[i] = stack.LastEmbeddingLevel;
                            if (stack.LastDirectionalOverrideStatus != ON)
                            {
                                _resultTypes[i] = stack.LastDirectionalOverrideStatus;
                            }
                        }

                        byte newLevel = isRtl
                            ? (byte)((stack.LastEmbeddingLevel + 1) | 1)   // least greater odd
                            : (byte)((stack.LastEmbeddingLevel + 2) & ~1); // least greater even

                        if (newLevel <= MaxDepth && overflowIsolateCount == 0 && overflowEmbeddingCount == 0)
                        {
                            if (isIsolate)
                            {
                                validIsolateCount++;
                            }

                            stack.Push(
                                newLevel,
                                t == LRO ? L : t == RLO ? R : ON,
                                isIsolate);

                            if (!isIsolate)
                            {
                                _resultLevels[i] = newLevel;
                            }
                        }
                        else
                        {
                            if (isIsolate)
                            {
                                overflowIsolateCount++;
                            }
                            else if (overflowIsolateCount == 0)
                            {
                                overflowEmbeddingCount++;
                            }
                        }

                        break;

                    case PDI: // Rule X6a
                        if (overflowIsolateCount > 0)
                        {
                            overflowIsolateCount--;
                        }
                        else if (validIsolateCount != 0)
                        {
                            overflowEmbeddingCount = 0;
                            while (!stack.LastDirectionalIsolateStatus)
                            {
                                stack.Pop();
                            }

                            stack.Pop();
                            validIsolateCount--;
                        }

                        _resultLevels[i] = stack.LastEmbeddingLevel;
                        if (stack.LastDirectionalOverrideStatus != ON)
                        {
                            _resultTypes[i] = stack.LastDirectionalOverrideStatus;
                        }

                        break;

                    case PDF: // Rule X7
                        _resultLevels[i] = stack.LastEmbeddingLevel;
                        if (overflowIsolateCount > 0)
                        {
                            // do nothing
                        }
                        else if (overflowEmbeddingCount > 0)
                        {
                            overflowEmbeddingCount--;
                        }
                        else if (!stack.LastDirectionalIsolateStatus && stack.Depth >= 2)
                        {
                            stack.Pop();
                        }

                        break;

                    case B: // Rule X8
                        stack.Empty();
                        overflowIsolateCount = 0;
                        overflowEmbeddingCount = 0;
                        validIsolateCount = 0;
                        _resultLevels[i] = _paragraphLevel;
                        break;

                    default: // Rule X6 (also BN, which X9 removes later)
                        _resultLevels[i] = stack.LastEmbeddingLevel;
                        if (stack.LastDirectionalOverrideStatus != ON)
                        {
                            _resultTypes[i] = stack.LastDirectionalOverrideStatus;
                        }

                        break;
                }
            }
        }

        // Rule X9: characters removed from isolating run sequences.
        private static bool IsRemovedByX9(byte biditype)
            => biditype is LRE or RLE or LRO or RLO or PDF or BN;

        private static byte TypeForLevel(int level) => (level & 1) == 0 ? L : R;

        private static void SetLevels(byte[] levels, int start, int limit, byte newLevel)
        {
            for (int i = start; i < limit; i++)
            {
                levels[i] = newLevel;
            }
        }

        // Determine level runs honouring X9 (removed characters are excluded).
        private List<int[]> DetermineLevelRuns()
        {
            var allRuns = new List<int[]>();
            var temporaryRun = new List<int>();
            int currentLevel = -1;

            for (int i = 0; i < _textLength; i++)
            {
                if (!IsRemovedByX9(_initialTypes[i]))
                {
                    if (_resultLevels[i] != currentLevel)
                    {
                        if (currentLevel >= 0)
                        {
                            allRuns.Add(temporaryRun.ToArray());
                            temporaryRun.Clear();
                        }

                        currentLevel = _resultLevels[i];
                    }

                    temporaryRun.Add(i);
                }
            }

            if (temporaryRun.Count != 0)
            {
                allRuns.Add(temporaryRun.ToArray());
            }

            return allRuns;
        }

        // Definition BD13.
        private List<IsolatingRunSequence> DetermineIsolatingRunSequences()
        {
            List<int[]> levelRuns = DetermineLevelRuns();
            int numRuns = levelRuns.Count;

            int[] runForCharacter = new int[_textLength];
            for (int runNumber = 0; runNumber < numRuns; runNumber++)
            {
                foreach (int characterIndex in levelRuns[runNumber])
                {
                    runForCharacter[characterIndex] = runNumber;
                }
            }

            var sequences = new List<IsolatingRunSequence>(numRuns);
            var currentRunSequence = new List<int>();
            for (int i = 0; i < numRuns; i++)
            {
                int firstCharacter = levelRuns[i][0];
                if (_initialTypes[firstCharacter] != PDI || _matchingIsolateInitiator[firstCharacter] == -1)
                {
                    currentRunSequence.Clear();
                    int run = i;
                    while (true)
                    {
                        currentRunSequence.AddRange(levelRuns[run]);

                        int lastCharacter = currentRunSequence[^1];
                        byte lastType = _initialTypes[lastCharacter];
                        if (lastType is LRI or RLI or FSI && _matchingPdi[lastCharacter] != _textLength)
                        {
                            run = runForCharacter[_matchingPdi[lastCharacter]];
                        }
                        else
                        {
                            break;
                        }
                    }

                    sequences.Add(new IsolatingRunSequence(this, currentRunSequence.ToArray()));
                }
            }

            return sequences;
        }

        // Assign arbitrary but level-run-preserving levels to X9-removed characters.
        private void AssignLevelsToCharactersRemovedByX9()
        {
            for (int i = 0; i < _textLength; i++)
            {
                byte t = _initialTypes[i];
                if (t is LRE or RLE or LRO or RLO or PDF or BN)
                {
                    _resultTypes[i] = t;
                    _resultLevels[i] = 0xFF; // sentinel for "unset"
                }
            }

            if (_resultLevels[0] == 0xFF)
            {
                _resultLevels[0] = _paragraphLevel;
            }

            for (int i = 1; i < _textLength; i++)
            {
                if (_resultLevels[i] == 0xFF)
                {
                    _resultLevels[i] = _resultLevels[i - 1];
                }
            }
        }

        // Rule L1: reset segment separators, paragraph separators and trailing whitespace
        // (including X9-removed format characters) to the paragraph level. Single line.
        private byte[] ComputeLineLevels()
        {
            byte[] result = (byte[])_resultLevels.Clone();

            for (int i = 0; i < _textLength; i++)
            {
                byte t = _initialTypes[i];
                if (t is B or S)
                {
                    result[i] = _paragraphLevel;
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (IsWhitespaceForL1(_initialTypes[j]))
                        {
                            result[j] = _paragraphLevel;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            for (int j = _textLength - 1; j >= 0; j--)
            {
                if (IsWhitespaceForL1(_initialTypes[j]))
                {
                    result[j] = _paragraphLevel;
                }
                else
                {
                    break;
                }
            }

            return result;
        }

        private static bool IsWhitespaceForL1(byte biditype)
            => biditype is LRE or RLE or LRO or RLO or PDF or LRI or RLI or FSI or PDI or BN or WS;

        // Rule L2: reorder a single line by level runs, returning a visual-to-logical map.
        private int[] ComputeReordering(byte[] levels)
        {
            int lineLength = levels.Length;
            int[] result = new int[lineLength];
            for (int i = 0; i < lineLength; i++)
            {
                result[i] = i;
            }

            byte highestLevel = 0;
            byte lowestOddLevel = MaxDepth + 2;
            for (int i = 0; i < lineLength; i++)
            {
                byte level = levels[i];
                if (level > highestLevel)
                {
                    highestLevel = level;
                }

                if ((level & 1) != 0 && level < lowestOddLevel)
                {
                    lowestOddLevel = level;
                }
            }

            for (int level = highestLevel; level >= lowestOddLevel; level--)
            {
                for (int i = 0; i < lineLength; i++)
                {
                    if (levels[i] >= level)
                    {
                        int start = i;
                        int limit = i + 1;
                        while (limit < lineLength && levels[limit] >= level)
                        {
                            limit++;
                        }

                        for (int j = start, k = limit - 1; j < k; j++, k--)
                        {
                            (result[j], result[k]) = (result[k], result[j]);
                        }

                        i = limit;
                    }
                }
            }

            return result;
        }

        private sealed class DirectionalStatusStack
        {
            private int _counter;
            private readonly byte[] _levels = new byte[MaxDepth + 1];
            private readonly byte[] _overrides = new byte[MaxDepth + 1];
            private readonly bool[] _isolates = new bool[MaxDepth + 1];

            public void Empty() => _counter = 0;

            public void Push(byte level, byte overrideStatus, bool isolateStatus)
            {
                _levels[_counter] = level;
                _overrides[_counter] = overrideStatus;
                _isolates[_counter] = isolateStatus;
                _counter++;
            }

            public void Pop() => _counter--;

            public int Depth => _counter;

            public byte LastEmbeddingLevel => _levels[_counter - 1];

            public byte LastDirectionalOverrideStatus => _overrides[_counter - 1];

            public bool LastDirectionalIsolateStatus => _isolates[_counter - 1];
        }

        // Rule X10: an isolating run sequence, over which W/N/I rules operate.
        private sealed class IsolatingRunSequence
        {
            private const byte PairNone = BidiCharacterData.PairedBracketNone;
            private const byte PairOpen = BidiCharacterData.PairedBracketOpen;
            private const byte PairClose = BidiCharacterData.PairedBracketClose;
            private const int MaxPairingDepth = 63;

            private readonly BidiParagraph _owner;
            private readonly int[] _indexes;
            private readonly byte[] _types;
            private readonly int _length;
            private readonly byte _level;
            private readonly byte _sos;
            private readonly byte _eos;
            private byte[] _resolvedLevels = Array.Empty<byte>();

            public IsolatingRunSequence(BidiParagraph owner, int[] inputIndexes)
            {
                _owner = owner;
                _indexes = inputIndexes;
                _length = inputIndexes.Length;

                _types = new byte[_length];
                for (int i = 0; i < _length; i++)
                {
                    _types[i] = owner._resultTypes[_indexes[i]];
                }

                _level = owner._resultLevels[_indexes[0]];

                int prevChar = _indexes[0] - 1;
                while (prevChar >= 0 && IsRemovedByX9(owner._initialTypes[prevChar]))
                {
                    prevChar--;
                }

                byte prevLevel = prevChar >= 0 ? owner._resultLevels[prevChar] : owner._paragraphLevel;
                _sos = TypeForLevel(Math.Max(prevLevel, _level));

                byte lastType = _types[_length - 1];
                byte succLevel;
                if (lastType is LRI or RLI or FSI)
                {
                    succLevel = owner._paragraphLevel;
                }
                else
                {
                    int limit = _indexes[_length - 1] + 1;
                    while (limit < owner._textLength && IsRemovedByX9(owner._initialTypes[limit]))
                    {
                        limit++;
                    }

                    succLevel = limit < owner._textLength ? owner._resultLevels[limit] : owner._paragraphLevel;
                }

                _eos = TypeForLevel(Math.Max(succLevel, _level));
            }

            // Rules W1-W7.
            public void ResolveWeakTypes()
            {
                // W1.
                byte preceding = _sos;
                for (int i = 0; i < _length; i++)
                {
                    byte t = _types[i];
                    if (t == NSM)
                    {
                        _types[i] = preceding is LRI or RLI or FSI or PDI ? ON : preceding;
                    }
                    else
                    {
                        preceding = t;
                    }
                }

                // W2.
                for (int i = 0; i < _length; i++)
                {
                    if (_types[i] == EN)
                    {
                        for (int j = i - 1; j >= 0; j--)
                        {
                            byte t = _types[j];
                            if (t is L or R or AL)
                            {
                                if (t == AL)
                                {
                                    _types[i] = AN;
                                }

                                break;
                            }
                        }
                    }
                }

                // W3.
                for (int i = 0; i < _length; i++)
                {
                    if (_types[i] == AL)
                    {
                        _types[i] = R;
                    }
                }

                // W4.
                for (int i = 1; i < _length - 1; i++)
                {
                    if (_types[i] is ES or CS)
                    {
                        byte prevSepType = _types[i - 1];
                        byte succSepType = _types[i + 1];
                        if (prevSepType == EN && succSepType == EN)
                        {
                            _types[i] = EN;
                        }
                        else if (_types[i] == CS && prevSepType == AN && succSepType == AN)
                        {
                            _types[i] = AN;
                        }
                    }
                }

                // W5.
                for (int i = 0; i < _length; i++)
                {
                    if (_types[i] == ET)
                    {
                        int runStart = i;
                        int runLimit = FindRunLimit(runStart, ET);

                        byte t = runStart == 0 ? _sos : _types[runStart - 1];
                        if (t != EN)
                        {
                            t = runLimit == _length ? _eos : _types[runLimit];
                        }

                        if (t == EN)
                        {
                            SetTypes(runStart, runLimit, EN);
                        }

                        i = runLimit;
                    }
                }

                // W6.
                for (int i = 0; i < _length; i++)
                {
                    if (_types[i] is ES or ET or CS)
                    {
                        _types[i] = ON;
                    }
                }

                // W7.
                for (int i = 0; i < _length; i++)
                {
                    if (_types[i] == EN)
                    {
                        byte prevStrongType = _sos;
                        for (int j = i - 1; j >= 0; j--)
                        {
                            byte t = _types[j];
                            if (t is L or R)
                            {
                                prevStrongType = t;
                                break;
                            }
                        }

                        if (prevStrongType == L)
                        {
                            _types[i] = L;
                        }
                    }
                }
            }

            // Rule N0: paired brackets (definitions BD14-BD16), a port of BidiPBAReference.
            public void ResolvePairedBrackets()
            {
                byte dirEmbed = (_level & 1) == 1 ? R : L;
                List<(int Opener, int Closer)> pairs = LocateBrackets();
                pairs.Sort(static (a, b) => a.Opener.CompareTo(b.Opener));
                foreach ((int opener, int closer) in pairs)
                {
                    AssignBracketType(opener, closer, dirEmbed);
                }
            }

            private List<(int Opener, int Closer)> LocateBrackets()
            {
                var openers = new List<int>();
                var pairs = new List<(int Opener, int Closer)>();

                for (int ich = 0; ich < _length; ich++)
                {
                    int originalIndex = _indexes[ich];
                    if (_owner._pairTypes[originalIndex] == PairNone || _types[ich] != ON)
                    {
                        continue;
                    }

                    switch (_owner._pairTypes[originalIndex])
                    {
                        case PairOpen:
                            if (openers.Count == MaxPairingDepth)
                            {
                                openers.Clear();
                                return pairs;
                            }

                            openers.Insert(0, ich);
                            break;

                        case PairClose:
                            if (openers.Count == 0)
                            {
                                continue;
                            }

                            for (int k = 0; k < openers.Count; k++)
                            {
                                int opener = openers[k];
                                if (_owner._pairValues[_indexes[opener]] == _owner._pairValues[originalIndex])
                                {
                                    pairs.Add((opener, ich));
                                    openers.RemoveRange(0, k + 1); // remove up to and including the match
                                    break;
                                }
                            }

                            break;
                    }
                }

                return pairs;
            }

            private byte GetStrongTypeN0(int ich)
            {
                return _types[ich] switch
                {
                    EN or AN or AL or R => R, // number types treated as R within N0
                    L => L,
                    _ => ON,
                };
            }

            private byte ClassifyPairContent(int opener, int closer, byte dirEmbed)
            {
                byte dirOpposite = ON;
                for (int ich = opener + 1; ich < closer; ich++)
                {
                    byte dir = GetStrongTypeN0(ich);
                    if (dir == ON)
                    {
                        continue;
                    }

                    if (dir == dirEmbed)
                    {
                        return dir;
                    }

                    dirOpposite = dir;
                }

                return dirOpposite;
            }

            private byte ClassBeforePair(int opener)
            {
                for (int ich = opener - 1; ich >= 0; ich--)
                {
                    byte dir = GetStrongTypeN0(ich);
                    if (dir != ON)
                    {
                        return dir;
                    }
                }

                return _sos;
            }

            private void AssignBracketType(int opener, int closer, byte dirEmbed)
            {
                byte dirPair = ClassifyPairContent(opener, closer, dirEmbed);
                if (dirPair == ON)
                {
                    return; // case d: no strong type inside
                }

                if (dirPair != dirEmbed)
                {
                    dirPair = ClassBeforePair(opener);
                    if (dirPair == dirEmbed || dirPair == ON)
                    {
                        dirPair = dirEmbed;
                    }
                }

                SetBracketsToType(opener, closer, dirPair);
            }

            private void SetBracketsToType(int opener, int closer, byte dirPair)
            {
                _types[opener] = dirPair;
                _types[closer] = dirPair;

                for (int i = opener + 1; i < closer; i++)
                {
                    if (_owner._initialTypes[_indexes[i]] == NSM)
                    {
                        _types[i] = dirPair;
                    }
                    else
                    {
                        break;
                    }
                }

                for (int i = closer + 1; i < _length; i++)
                {
                    if (_owner._initialTypes[_indexes[i]] == NSM)
                    {
                        _types[i] = dirPair;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // Rules N1-N2.
            public void ResolveNeutralTypes()
            {
                for (int i = 0; i < _length; i++)
                {
                    byte t = _types[i];
                    if (t is WS or ON or B or S or RLI or LRI or FSI or PDI)
                    {
                        int runStart = i;
                        int runLimit = FindRunLimit(runStart, B, S, WS, ON, RLI, LRI, FSI, PDI);

                        byte leadingType;
                        if (runStart == 0)
                        {
                            leadingType = _sos;
                        }
                        else
                        {
                            leadingType = _types[runStart - 1];
                            if (leadingType is AN or EN)
                            {
                                leadingType = R;
                            }
                        }

                        byte trailingType;
                        if (runLimit == _length)
                        {
                            trailingType = _eos;
                        }
                        else
                        {
                            trailingType = _types[runLimit];
                            if (trailingType is AN or EN)
                            {
                                trailingType = R;
                            }
                        }

                        byte resolvedType = leadingType == trailingType
                            ? leadingType          // N1
                            : TypeForLevel(_level); // N2

                        SetTypes(runStart, runLimit, resolvedType);
                        i = runLimit;
                    }
                }
            }

            // Rules I1, I2.
            public void ResolveImplicitLevels()
            {
                _resolvedLevels = new byte[_length];
                SetLevels(_resolvedLevels, 0, _length, _level);

                if ((_level & 1) == 0)
                {
                    for (int i = 0; i < _length; i++)
                    {
                        byte t = _types[i];
                        if (t == L)
                        {
                            // no change
                        }
                        else if (t == R)
                        {
                            _resolvedLevels[i] += 1;
                        }
                        else
                        {
                            _resolvedLevels[i] += 2; // AN, EN
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < _length; i++)
                    {
                        byte t = _types[i];
                        if (t == R)
                        {
                            // no change
                        }
                        else
                        {
                            _resolvedLevels[i] += 1; // L, AN, EN
                        }
                    }
                }
            }

            public void ApplyLevelsAndTypes()
            {
                for (int i = 0; i < _length; i++)
                {
                    int originalIndex = _indexes[i];
                    _owner._resultTypes[originalIndex] = _types[i];
                    _owner._resultLevels[originalIndex] = _resolvedLevels[i];
                }
            }

            private int FindRunLimit(int index, params byte[] validSet)
            {
                while (index < _length)
                {
                    byte t = _types[index];
                    bool matched = false;
                    for (int i = 0; i < validSet.Length; i++)
                    {
                        if (t == validSet[i])
                        {
                            matched = true;
                            break;
                        }
                    }

                    if (!matched)
                    {
                        return index;
                    }

                    index++;
                }

                return _length;
            }

            private void SetTypes(int start, int limit, byte newType)
            {
                for (int i = start; i < limit; i++)
                {
                    _types[i] = newType;
                }
            }
        }
    }
}
