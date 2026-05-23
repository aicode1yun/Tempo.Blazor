using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict tests for the new text measurement and line breaking engine.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase5E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DocumentEditor_Strict_TextLayout_MeasurementCacheUsesFullFontKeyAndInvalidates()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<TextMeasurementProbe>(
            """
            () => {
                const api = window.tmDocumentEditorEngine.textLayout;
                const service = api.createTextMeasurementService({ zoom: 1 });
                const style = {
                    fontFamily: 'Arial',
                    fontSize: 16,
                    fontWeight: '400',
                    fontStyle: 'normal',
                    letterSpacing: 0
                };
                const first = service.measureText('Cache key text', style);
                const second = service.measureText('Cache key text', style);
                const bold = service.measureText('Cache key text', { ...style, fontWeight: '700' });
                const italic = service.measureText('Cache key text', { ...style, fontStyle: 'italic' });
                const spaced = service.measureText('Cache key text', { ...style, letterSpacing: 1.5 });
                const family = service.measureText('Cache key text', { ...style, fontFamily: 'Times New Roman' });
                const size = service.measureText('Cache key text', { ...style, fontSize: 22 });
                const beforeInvalidate = service.getStats();
                service.setZoom(1.25);
                const zoomed = service.measureText('Cache key text', style);
                const afterZoom = service.getStats();
                service.invalidate('test');
                service.measureText('Cache key text', style);
                const afterInvalidate = service.getStats();

                return {
                    firstWidth: first.width,
                    repeatedWidth: second.width,
                    boldWidth: bold.width,
                    italicWidth: italic.width,
                    spacedWidth: spaced.width,
                    familyWidth: family.width,
                    sizeWidth: size.width,
                    zoomedWidth: zoomed.width,
                    hitsBeforeInvalidate: beforeInvalidate.cacheHits,
                    missesBeforeInvalidate: beforeInvalidate.cacheMisses,
                    invalidationsAfterZoom: afterZoom.invalidations,
                    invalidationsAfterManual: afterInvalidate.invalidations,
                    cacheEntriesAfterManual: afterInvalidate.cacheEntries,
                    canvasAvailable: afterInvalidate.canvasAvailable
                };
            }
            """);

        result.FirstWidth.Should().BeGreaterThan(0);
        result.RepeatedWidth.Should().Be(result.FirstWidth);
        result.HitsBeforeInvalidate.Should().BeGreaterThanOrEqualTo(1);
        result.MissesBeforeInvalidate.Should().BeGreaterThanOrEqualTo(6);
        result.BoldWidth.Should().BeGreaterThan(0);
        result.ItalicWidth.Should().BeGreaterThan(0);
        result.SpacedWidth.Should().BeGreaterThan(result.FirstWidth);
        result.FamilyWidth.Should().BeGreaterThan(0);
        result.SizeWidth.Should().BeGreaterThan(result.FirstWidth);
        result.ZoomedWidth.Should().BeGreaterThan(result.FirstWidth);
        result.InvalidationsAfterZoom.Should().BeGreaterThanOrEqualTo(1);
        result.InvalidationsAfterManual.Should().BeGreaterThan(result.InvalidationsAfterZoom);
        result.CacheEntriesAfterManual.Should().Be(1);
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_TextLayout_TokenizesAllRequiredTokenTypes()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<TokenizationProbe>(
            """
            () => {
                const tokens = window.tmDocumentEditorEngine.textLayout.tokenizeText(
                    'word  next\n\tsoft\u00adhyphen\u00a0中文supercalifragilisticexpialidociouslongtoken'
                );
                const types = Array.from(new Set(tokens.map(token => token.type)));
                const longToken = tokens.find(token => token.type === 'longToken');
                const cjkTokens = tokens.filter(token => token.type === 'cjk');
                return {
                    tokenCount: tokens.length,
                    types,
                    hasWord: types.includes('word'),
                    hasSpace: types.includes('space'),
                    hasNewline: types.includes('newline'),
                    hasTab: types.includes('tab'),
                    hasSoftHyphen: types.includes('softHyphen'),
                    hasNbsp: types.includes('nbsp'),
                    hasLongToken: types.includes('longToken'),
                    cjkCount: cjkTokens.length,
                    longTokenText: String(longToken?.text || ''),
                    monotonicOffsets: tokens.every((token, index) => index === 0 || token.start >= tokens[index - 1].end)
                };
            }
            """);

        result.TokenCount.Should().BeGreaterThan(8);
        result.HasWord.Should().BeTrue();
        result.HasSpace.Should().BeTrue();
        result.HasNewline.Should().BeTrue();
        result.HasTab.Should().BeTrue();
        result.HasSoftHyphen.Should().BeTrue();
        result.HasNbsp.Should().BeTrue();
        result.HasLongToken.Should().BeTrue();
        result.CjkCount.Should().Be(2);
        result.LongTokenText.Should().Contain("supercalifragilistic");
        result.MonotonicOffsets.Should().BeTrue();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_TextLayout_GreedyLineBreakerRespectsIntervalsHardBreaksAndLongTokens()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<LineBreakerProbe>(
            """
            () => {
                const api = window.tmDocumentEditorEngine.textLayout;
                const service = api.createTextMeasurementService();
                const breaker = api.createLineBreaker(service);
                const layout = breaker.breakParagraph({
                    runs: [
                        { text: 'Alpha beta ', style: { fontSize: 16 } },
                        { text: 'GAMMA', marks: [{ type: 'Bold' }], style: { fontSize: 20 } },
                        { text: '\nSupercalifragilisticexpialidociousWrapToken omega', style: { fontSize: 16 } }
                    ],
                    style: { fontFamily: 'Arial', fontSize: 16 },
                    alignment: 'left'
                }, {
                    x: 20,
                    y: 30,
                    width: 150,
                    lineGap: 2,
                    minReadableWidth: 40,
                    availableIntervals: [{ x: 20, width: 150 }, { x: 40, width: 120 }]
                });
                const allSegmentsInsideIntervals = layout.lines.every(line => {
                    const interval = line.availableIntervals[0];
                    return line.segments.every(segment =>
                        segment.rect.x >= interval.x - 0.1
                        && segment.rect.x + segment.rect.width <= interval.x + interval.width + 0.1);
                });
                const splitSegments = layout.segments.filter(segment => segment.splitFromLongToken === true);
                return {
                    ok: layout.ok === true,
                    lineCount: layout.lines.length,
                    segmentCount: layout.segments.length,
                    hasHardBreakLine: layout.lines.some(line => line.hardBreak === true),
                    splitLongTokenCount: splitSegments.length,
                    mixedFontLineHeight: Math.max(...layout.lines.map(line => line.rect.height)),
                    allSegmentsInsideIntervals,
                    firstLineStart: Number(layout.lines[0]?.start ?? -1),
                    lastLineEnd: Number(layout.lines[layout.lines.length - 1]?.end ?? -1),
                    caretStopCount: layout.caretStops.length,
                    fallback: layout.fallback === true
                };
            }
            """);

        result.Ok.Should().BeTrue();
        result.Fallback.Should().BeFalse();
        result.LineCount.Should().BeGreaterThan(2);
        result.SegmentCount.Should().BeGreaterThan(4);
        result.HasHardBreakLine.Should().BeTrue();
        result.SplitLongTokenCount.Should().BeGreaterThan(0);
        result.MixedFontLineHeight.Should().BeGreaterThan(20);
        result.AllSegmentsInsideIntervals.Should().BeTrue();
        result.FirstLineStart.Should().Be(0);
        result.LastLineEnd.Should().BeGreaterThan(50);
        result.CaretStopCount.Should().BeGreaterThan(10);
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_TextLayout_JustifyIsLayoutMetadataAndDoesNotMoveLogicalOffsets()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<JustifyProbe>(
            """
            () => {
                const api = window.tmDocumentEditorEngine.textLayout;
                const service = api.createTextMeasurementService();
                const breaker = api.createLineBreaker(service);
                const layout = breaker.breakParagraph({
                    text: 'One two three four five six seven eight nine ten',
                    style: { fontFamily: 'Arial', fontSize: 16 },
                    alignment: 'justify'
                }, {
                    x: 0,
                    y: 0,
                    width: 170,
                    minReadableWidth: 60
                });
                const beforeOffsets = layout.lines.map(line => `${line.start}:${line.end}`).join('|');
                const justifiedLines = layout.lines.filter(line => line.justify?.enabled === true);
                const lastLine = layout.lines[layout.lines.length - 1];
                return {
                    ok: layout.ok === true,
                    lineCount: layout.lines.length,
                    justifiedCount: justifiedLines.length,
                    lastLineJustified: lastLine?.justify?.enabled === true,
                    hardBreakJustified: layout.lines.some(line => line.hardBreak === true && line.justify?.enabled === true),
                    offsetFingerprint: beforeOffsets,
                    formattingStateTouched: layout.formattingStateTouched === true,
                    extraSpacePositive: justifiedLines.every(line => line.justify.extraSpacePerGap > 0)
                };
            }
            """);

        result.Ok.Should().BeTrue();
        result.LineCount.Should().BeGreaterThan(1);
        result.JustifiedCount.Should().BeGreaterThan(0);
        result.LastLineJustified.Should().BeFalse();
        result.HardBreakJustified.Should().BeFalse();
        result.OffsetFingerprint.Should().NotBeNullOrWhiteSpace();
        result.FormattingStateTouched.Should().BeFalse();
        result.ExtraSpacePositive.Should().BeTrue();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_TextLayout_FailSafeReturnsNonOverlappingFallbackForZeroWidthInterval()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<FailSafeProbe>(
            """
            () => {
                const api = window.tmDocumentEditorEngine.textLayout;
                const service = api.createTextMeasurementService();
                const breaker = api.createLineBreaker(service);
                const layout = breaker.breakParagraph({
                    text: 'Fallback text must stay readable when no usable interval exists.',
                    style: { fontFamily: 'Arial', fontSize: 16 }
                }, {
                    x: 10,
                    y: 10,
                    width: 0,
                    minReadableWidth: 60,
                    availableIntervals: [{ x: 10, y: 10, width: 0, height: 24 }]
                });
                const nonOverlapping = layout.lines.every((line, index) =>
                    index === 0 || line.rect.y >= layout.lines[index - 1].rect.y + layout.lines[index - 1].rect.height);
                return {
                    ok: layout.ok === true,
                    fallback: layout.fallback === true,
                    reason: String(layout.debug?.fallbackReason || ''),
                    lineCount: layout.lines.length,
                    nonOverlapping,
                    safeWidth: Number(layout.lines[0]?.rect.width ?? 0),
                    safeY: Number(layout.lines[0]?.rect.y ?? 0)
                };
            }
            """);

        result.Ok.Should().BeTrue();
        result.Fallback.Should().BeTrue();
        result.Reason.Should().Be("invalid-available-interval");
        result.LineCount.Should().BeGreaterThan(0);
        result.NonOverlapping.Should().BeTrue();
        result.SafeWidth.Should().BeGreaterThanOrEqualTo(60);
        result.SafeY.Should().BeGreaterThan(10);
    }

    public sealed class TextMeasurementProbe
    {
        [JsonPropertyName("firstWidth")] public double FirstWidth { get; set; }
        [JsonPropertyName("repeatedWidth")] public double RepeatedWidth { get; set; }
        [JsonPropertyName("boldWidth")] public double BoldWidth { get; set; }
        [JsonPropertyName("italicWidth")] public double ItalicWidth { get; set; }
        [JsonPropertyName("spacedWidth")] public double SpacedWidth { get; set; }
        [JsonPropertyName("familyWidth")] public double FamilyWidth { get; set; }
        [JsonPropertyName("sizeWidth")] public double SizeWidth { get; set; }
        [JsonPropertyName("zoomedWidth")] public double ZoomedWidth { get; set; }
        [JsonPropertyName("hitsBeforeInvalidate")] public int HitsBeforeInvalidate { get; set; }
        [JsonPropertyName("missesBeforeInvalidate")] public int MissesBeforeInvalidate { get; set; }
        [JsonPropertyName("invalidationsAfterZoom")] public int InvalidationsAfterZoom { get; set; }
        [JsonPropertyName("invalidationsAfterManual")] public int InvalidationsAfterManual { get; set; }
        [JsonPropertyName("cacheEntriesAfterManual")] public int CacheEntriesAfterManual { get; set; }
        [JsonPropertyName("canvasAvailable")] public bool CanvasAvailable { get; set; }
    }

    public sealed class TokenizationProbe
    {
        [JsonPropertyName("tokenCount")] public int TokenCount { get; set; }
        [JsonPropertyName("types")] public string[] Types { get; set; } = [];
        [JsonPropertyName("hasWord")] public bool HasWord { get; set; }
        [JsonPropertyName("hasSpace")] public bool HasSpace { get; set; }
        [JsonPropertyName("hasNewline")] public bool HasNewline { get; set; }
        [JsonPropertyName("hasTab")] public bool HasTab { get; set; }
        [JsonPropertyName("hasSoftHyphen")] public bool HasSoftHyphen { get; set; }
        [JsonPropertyName("hasNbsp")] public bool HasNbsp { get; set; }
        [JsonPropertyName("hasLongToken")] public bool HasLongToken { get; set; }
        [JsonPropertyName("cjkCount")] public int CjkCount { get; set; }
        [JsonPropertyName("longTokenText")] public string LongTokenText { get; set; } = string.Empty;
        [JsonPropertyName("monotonicOffsets")] public bool MonotonicOffsets { get; set; }
    }

    public sealed class LineBreakerProbe
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("lineCount")] public int LineCount { get; set; }
        [JsonPropertyName("segmentCount")] public int SegmentCount { get; set; }
        [JsonPropertyName("hasHardBreakLine")] public bool HasHardBreakLine { get; set; }
        [JsonPropertyName("splitLongTokenCount")] public int SplitLongTokenCount { get; set; }
        [JsonPropertyName("mixedFontLineHeight")] public double MixedFontLineHeight { get; set; }
        [JsonPropertyName("allSegmentsInsideIntervals")] public bool AllSegmentsInsideIntervals { get; set; }
        [JsonPropertyName("firstLineStart")] public int FirstLineStart { get; set; }
        [JsonPropertyName("lastLineEnd")] public int LastLineEnd { get; set; }
        [JsonPropertyName("caretStopCount")] public int CaretStopCount { get; set; }
        [JsonPropertyName("fallback")] public bool Fallback { get; set; }
    }

    public sealed class JustifyProbe
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("lineCount")] public int LineCount { get; set; }
        [JsonPropertyName("justifiedCount")] public int JustifiedCount { get; set; }
        [JsonPropertyName("lastLineJustified")] public bool LastLineJustified { get; set; }
        [JsonPropertyName("hardBreakJustified")] public bool HardBreakJustified { get; set; }
        [JsonPropertyName("offsetFingerprint")] public string OffsetFingerprint { get; set; } = string.Empty;
        [JsonPropertyName("formattingStateTouched")] public bool FormattingStateTouched { get; set; }
        [JsonPropertyName("extraSpacePositive")] public bool ExtraSpacePositive { get; set; }
    }

    public sealed class FailSafeProbe
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("fallback")] public bool Fallback { get; set; }
        [JsonPropertyName("reason")] public string Reason { get; set; } = string.Empty;
        [JsonPropertyName("lineCount")] public int LineCount { get; set; }
        [JsonPropertyName("nonOverlapping")] public bool NonOverlapping { get; set; }
        [JsonPropertyName("safeWidth")] public double SafeWidth { get; set; }
        [JsonPropertyName("safeY")] public double SafeY { get; set; }
    }
}
