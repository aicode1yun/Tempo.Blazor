using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Xlsx;

/// <summary>Imports an XLSX file into a <see cref="SpreadsheetWorkbook"/>.</summary>
public static class XlsxImporter
{
    /// <summary>Imports the given XLSX data and returns a new workbook.</summary>
    public static SpreadsheetWorkbook Import(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var doc = SpreadsheetDocument.Open(stream, false);
        var workbookPart = doc.WorkbookPart ?? throw new InvalidDataException("Missing workbook part.");
        var sst = workbookPart.GetPartsOfType<SharedStringTablePart>().FirstOrDefault();
        var stylesPart = workbookPart.GetPartsOfType<WorkbookStylesPart>().FirstOrDefault();
        var styleMap = stylesPart is not null ? BuildStyleMap(stylesPart) : new Dictionary<int, SpreadsheetCellStyle>();

        var workbook = new SpreadsheetWorkbook();
        workbook.Sheets.Clear();

        var sheets = workbookPart.Workbook.Sheets?.Elements<Sheet>() ?? Enumerable.Empty<Sheet>();
        foreach (var sheet in sheets)
        {
            if (sheet.Id?.Value is null) continue;
            var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id.Value);
            var worksheet = worksheetPart.Worksheet;
            var sheetData = worksheet.Elements<SheetData>().FirstOrDefault();
            var mergeCells = worksheet.Elements<MergeCells>().FirstOrDefault();
            var columns = worksheet.Elements<Columns>().FirstOrDefault();

            var ss = new SpreadsheetSheet { Name = sheet.Name?.Value ?? "Sheet" };

            // Column widths and hidden state
            if (columns is not null)
            {
                foreach (var col in columns.Elements<Column>())
                {
                    if (col.Min is null || col.Max is null) continue;
                    for (uint c = col.Min.Value; c <= col.Max.Value; c++)
                    {
                        var idx = (int)(c - 1);
                        if (!ss.Columns.TryGetValue(idx, out var sc))
                        {
                            sc = new SpreadsheetColumn { Index = idx };
                            ss.Columns[idx] = sc;
                        }
                        var width = col.Width?.Value;
                        if (width is not null) sc.Width = width * 7.0;
                        if (col.Hidden?.Value == true) sc.IsHidden = true;
                    }
                }
            }

            // Row heights and cells
            if (sheetData is not null)
            {
                foreach (var row in sheetData.Elements<Row>())
                {
                    var rowIndex = (int)(row.RowIndex?.Value ?? 0) - 1;
                    if (rowIndex < 0) continue;

                    if (row.Height is not null || row.Hidden?.Value == true)
                    {
                        if (!ss.Rows.TryGetValue(rowIndex, out var r))
                        {
                            r = new SpreadsheetRow { Index = rowIndex };
                            ss.Rows[rowIndex] = r;
                        }
                        if (row.Height is not null) r.Height = row.Height.Value;
                        if (row.Hidden?.Value == true) r.IsHidden = true;
                    }

                    foreach (var cell in row.Elements<Cell>())
                    {
                        var cellRef = cell.CellReference?.Value;
                        if (cellRef is null) continue;

                        var sc = new SpreadsheetCell();

                        // Value / formula
                        if (cell.CellFormula is not null)
                        {
                            sc.Formula = cell.CellFormula.Text;
                            sc.Value = cell.CellValue?.Text;
                            sc.DataType = SpreadsheetDataType.Text;
                        }
                        else
                        {
                            var raw = cell.CellValue?.Text;
                            sc.Value = ConvertValue(raw, cell.DataType?.Value, sst);
                            sc.DataType = InferDataType(cell.DataType?.Value);
                        }

                        // Style
                        if (cell.StyleIndex is not null && styleMap.TryGetValue((int)cell.StyleIndex.Value, out var mappedStyle))
                            sc.Style = mappedStyle.Clone();

                        ss.Cells[cellRef] = sc;
                    }
                }
            }

            // Merged cells
            if (mergeCells is not null)
            {
                foreach (var mc in mergeCells.Elements<MergeCell>())
                {
                    if (mc.Reference?.Value is null) continue;
                    try
                    {
                        ss.MergedCells.Add(SpreadsheetRange.Parse(mc.Reference.Value));
                    }
                    catch { /* ignore invalid merge refs */ }
                }
            }

            // Auto-filter definition
            var autoFilter = worksheet.Elements<AutoFilter>().FirstOrDefault();
            if (autoFilter?.Reference?.Value is { } filterRef)
            {
                try
                {
                    ss.AutoFilter = new SpreadsheetAutoFilter(SpreadsheetRange.Parse(filterRef));
                }
                catch { /* ignore invalid filter refs */ }
            }

            // Data validation rules
            var dataValidations = worksheet.Elements<DataValidations>().FirstOrDefault();
            if (dataValidations is not null)
            {
                foreach (var dv in dataValidations.Elements<DataValidation>())
                {
                    try
                    {
                        var sqRef = dv.SequenceOfReferences?.InnerText ?? dv.GetAttribute("sqref", "").Value;
                        if (string.IsNullOrEmpty(sqRef)) continue;
                        var range = SpreadsheetRange.Parse(sqRef.Split(' ')[0]);
                        var rule = new SpreadsheetDataValidation
                        {
                            Range = range,
                            Type = ParseValidationType(dv.Type?.Value),
                            Operator = ParseValidationOperator(dv.Operator?.Value),
                            Formula1 = dv.Formula1?.Text,
                            Formula2 = dv.Formula2?.Text,
                            AllowBlank = dv.AllowBlank?.Value ?? false,
                            ShowDropDown = !(dv.ShowDropDown?.Value ?? false)
                        };
                        if (dv.PromptTitle?.Value is { Length: > 0 } ptitle || dv.Prompt?.Value is { Length: > 0 } pmsg)
                            rule = rule with { InputMessage = new SpreadsheetInputMessage { Title = dv.PromptTitle?.Value, Message = dv.Prompt?.Value } };
                        if (dv.Error?.Value is { Length: > 0 } || dv.ErrorTitle?.Value is { Length: > 0 })
                            rule = rule with { ErrorAlert = new SpreadsheetValidationErrorAlert { Style = ParseErrorStyle(dv.ErrorStyle?.Value), Title = dv.ErrorTitle?.Value, Message = dv.Error?.Value } };
                        ss.DataValidations.Add(rule);
                    }
                    catch { /* ignore malformed rules */ }
                }
            }

            // Hyperlinks
            var hyperlinks = worksheet.Elements<Hyperlinks>().FirstOrDefault();
            if (hyperlinks is not null)
            {
                foreach (var hl in hyperlinks.Elements<Hyperlink>())
                {
                    try
                    {
                        var cellRef = hl.Reference?.Value ?? string.Empty;
                        if (string.IsNullOrEmpty(cellRef)) continue;

                        var link = new SpreadsheetHyperlink();
                        if (hl.Location?.Value is { Length: > 0 } loc)
                        {
                            link.Kind = SpreadsheetHyperlinkKind.InternalRef;
                            link.Target = loc;
                        }
                        else if (hl.Id?.Value is { Length: > 0 } relId)
                        {
                            var rel = worksheetPart.HyperlinkRelationships.FirstOrDefault(r => r.Id == relId);
                            if (rel?.Uri?.ToString() is { Length: > 0 } uri)
                            {
                                if (uri.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                                {
                                    link.Kind = SpreadsheetHyperlinkKind.Email;
                                    link.Target = uri[7..];
                                    if (link.Target.Contains('?'))
                                    {
                                        var parts = link.Target.Split('?');
                                        link.Target = Uri.UnescapeDataString(parts[0]);
                                        var qs = parts[1];
                                        if (qs.StartsWith("subject="))
                                            link.Display = Uri.UnescapeDataString(qs[8..]);
                                    }
                                }
                                else
                                {
                                    link.Kind = SpreadsheetHyperlinkKind.Web;
                                    link.Target = uri;
                                }
                            }
                        }

                        // Don't overwrite a Display already derived from a mailto subject with a null/empty xlsx value.
                        if (hl.Display?.Value is { Length: > 0 } display)
                            link.Display = display;
                        link.Tooltip = hl.Tooltip?.Value;

                        if (!string.IsNullOrEmpty(link.Target))
                        {
                            if (!ss.Cells.TryGetValue(cellRef, out var cell))
                                cell = new SpreadsheetCell();
                            cell.Hyperlink = link;
                            ss.Cells[cellRef] = cell;
                        }
                    }
                    catch { /* ignore malformed hyperlinks */ }
                }
            }

            workbook.Sheets.Add(ss);
        }

        if (workbook.Sheets.Count == 0)
            workbook.AddSheet("Sheet1");

        for (int i = 0; i < workbook.Sheets.Count; i++)
        {
            workbook.Sheets[i].Workbook = workbook;
            workbook.Sheets[i].SheetIndexInWorkbook = i;
        }

        // Defined names (named ranges)
        var definedNames = workbookPart.Workbook.DefinedNames;
        if (definedNames is not null)
        {
            foreach (var dn in definedNames.Elements<DefinedName>())
            {
                try
                {
                    var name = dn.Name?.Value ?? string.Empty;
                    var refersTo = dn.Text ?? string.Empty;
                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(refersTo)) continue;

                    var scope = NamedRangeScope.Workbook;
                    int? sheetIndex = null;
                    if (dn.LocalSheetId?.Value is { } localIdU && (int)localIdU >= 0 && (int)localIdU < workbook.Sheets.Count)
                    {
                        scope = NamedRangeScope.Sheet;
                        sheetIndex = (int)localIdU;
                    }

                    workbook.NamedRanges.Add(new SpreadsheetNamedRange
                    {
                        Name = name,
                        RefersTo = refersTo,
                        Scope = scope,
                        SheetIndex = sheetIndex,
                        Comment = dn.Comment?.Value
                    });
                }
                catch { /* ignore malformed defined names */ }
            }
        }

        return workbook;
    }

    private static object? ConvertValue(string? raw, CellValues? dataType, SharedStringTablePart? sst)
    {
        if (raw is null) return null;
        if (dataType == CellValues.SharedString && sst?.SharedStringTable is not null)
        {
            if (int.TryParse(raw, out var idx) && idx >= 0 && idx < sst.SharedStringTable.ChildElements.Count)
                return sst.SharedStringTable.ChildElements[idx].InnerText;
            return raw;
        }
        if (dataType == CellValues.Boolean)
            return raw == "1";
        if (dataType == CellValues.Number && double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return d;
        if (dataType == CellValues.Date && double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var od))
            return DateTime.FromOADate(od);
        return raw;
    }

    private static SpreadsheetValidationType ParseValidationType(DataValidationValues? v)
    {
        if (v == DataValidationValues.Whole) return SpreadsheetValidationType.Whole;
        if (v == DataValidationValues.Decimal) return SpreadsheetValidationType.Decimal;
        if (v == DataValidationValues.List) return SpreadsheetValidationType.List;
        if (v == DataValidationValues.Date) return SpreadsheetValidationType.Date;
        if (v == DataValidationValues.Time) return SpreadsheetValidationType.Time;
        if (v == DataValidationValues.TextLength) return SpreadsheetValidationType.TextLength;
        if (v == DataValidationValues.Custom) return SpreadsheetValidationType.Custom;
        return SpreadsheetValidationType.Any;
    }

    private static SpreadsheetValidationOperator ParseValidationOperator(DataValidationOperatorValues? v)
    {
        if (v == DataValidationOperatorValues.Between) return SpreadsheetValidationOperator.Between;
        if (v == DataValidationOperatorValues.NotBetween) return SpreadsheetValidationOperator.NotBetween;
        if (v == DataValidationOperatorValues.Equal) return SpreadsheetValidationOperator.Equal;
        if (v == DataValidationOperatorValues.NotEqual) return SpreadsheetValidationOperator.NotEqual;
        if (v == DataValidationOperatorValues.GreaterThan) return SpreadsheetValidationOperator.GreaterThan;
        if (v == DataValidationOperatorValues.LessThan) return SpreadsheetValidationOperator.LessThan;
        if (v == DataValidationOperatorValues.GreaterThanOrEqual) return SpreadsheetValidationOperator.GreaterOrEqual;
        if (v == DataValidationOperatorValues.LessThanOrEqual) return SpreadsheetValidationOperator.LessOrEqual;
        return SpreadsheetValidationOperator.Between;
    }

    private static SpreadsheetValidationErrorStyle ParseErrorStyle(DataValidationErrorStyleValues? v)
    {
        if (v == DataValidationErrorStyleValues.Warning) return SpreadsheetValidationErrorStyle.Warning;
        if (v == DataValidationErrorStyleValues.Information) return SpreadsheetValidationErrorStyle.Information;
        return SpreadsheetValidationErrorStyle.Stop;
    }

    private static SpreadsheetDataType InferDataType(CellValues? dataType)
    {
        if (dataType == CellValues.Number) return SpreadsheetDataType.Number;
        if (dataType == CellValues.Boolean) return SpreadsheetDataType.Boolean;
        if (dataType == CellValues.Date) return SpreadsheetDataType.Date;
        return SpreadsheetDataType.Text;
    }

    private static Dictionary<int, SpreadsheetCellStyle> BuildStyleMap(WorkbookStylesPart stylesPart)
    {
        var map = new Dictionary<int, SpreadsheetCellStyle>();
        var stylesheet = stylesPart.Stylesheet;
        if (stylesheet is null) return map;

        var fonts = stylesheet.Fonts?.Elements<Font>().ToList() ?? new List<Font>();
        var fills = stylesheet.Fills?.Elements<Fill>().ToList() ?? new List<Fill>();
        var borders = stylesheet.Borders?.Elements<Border>().ToList() ?? new List<Border>();
        var cellFormats = stylesheet.CellFormats?.Elements<CellFormat>().ToList() ?? new List<CellFormat>();
        var numFmts = stylesheet.NumberingFormats?.Elements<NumberingFormat>().ToList() ?? new List<NumberingFormat>();

        for (int i = 0; i < cellFormats.Count; i++)
        {
            var cf = cellFormats[i];
            var style = new SpreadsheetCellStyle();

            // Font
            if (cf.FontId is not null && cf.FontId.Value < fonts.Count)
            {
                var font = fonts[(int)cf.FontId.Value];
                style.Bold = font.Bold is not null;
                style.Italic = font.Italic is not null;
                style.Underline = font.Underline is not null;
                if (font.FontSize?.Val is not null)
                    style.FontSize = font.FontSize.Val.Value;
                if (font.Color?.Rgb is not null)
                    style.ForeColor = $"#{font.Color.Rgb.Value}";
                else if (font.Color?.Theme is not null)
                    style.ForeColor = ThemeColorToHex(font.Color.Theme.Value, font.Color.Tint?.Value);
                if (font.FontName?.Val is not null)
                    style.FontFamily = font.FontName.Val.Value;
            }

            // Fill
            if (cf.FillId is not null && cf.FillId.Value < fills.Count)
            {
                var fill = fills[(int)cf.FillId.Value];
                if (fill.PatternFill?.ForegroundColor?.Rgb is not null)
                    style.BackgroundColor = $"#{fill.PatternFill.ForegroundColor.Rgb.Value}";
                else if (fill.PatternFill?.ForegroundColor?.Theme is not null)
                    style.BackgroundColor = ThemeColorToHex(fill.PatternFill.ForegroundColor.Theme.Value, fill.PatternFill.ForegroundColor.Tint?.Value);
            }

            // Alignment
            if (cf.Alignment is not null)
            {
                style.HorizontalAlign = ParseHorizontalAlign(cf.Alignment.Horizontal?.Value);
                style.VerticalAlign = ParseVerticalAlign(cf.Alignment.Vertical?.Value);
                style.TextWrap = cf.Alignment.WrapText?.Value == true;
            }

            // Number format
            if (cf.NumberFormatId is not null)
            {
                var nfId = (int)cf.NumberFormatId.Value;
                var custom = numFmts.FirstOrDefault(n => n.NumberFormatId?.Value == nfId);
                style.NumberFormat = custom?.FormatCode?.Value ?? BuiltinNumberFormat(nfId);
            }

            // Borders
            if (cf.BorderId is not null && cf.BorderId.Value < borders.Count)
            {
                var border = borders[(int)cf.BorderId.Value];
                style.BorderTop = MapBorder(border.TopBorder);
                style.BorderRight = MapBorder(border.RightBorder);
                style.BorderBottom = MapBorder(border.BottomBorder);
                style.BorderLeft = MapBorder(border.LeftBorder);
            }

            map[i] = style;
        }

        return map;
    }

    private static SpreadsheetBorder MapBorder(OpenXmlElement? edge)
    {
        if (edge is not BorderPropertiesType bpt || bpt.Style?.Value is null || bpt.Style.Value == BorderStyleValues.None)
            return new SpreadsheetBorder(SpreadsheetBorderStyle.None, "#000000");

        SpreadsheetBorderStyle style;
        if (bpt.Style.Value == BorderStyleValues.Thin) style = SpreadsheetBorderStyle.Thin;
        else if (bpt.Style.Value == BorderStyleValues.Medium) style = SpreadsheetBorderStyle.Medium;
        else if (bpt.Style.Value == BorderStyleValues.Thick) style = SpreadsheetBorderStyle.Thick;
        else if (bpt.Style.Value == BorderStyleValues.Dashed) style = SpreadsheetBorderStyle.Dashed;
        else if (bpt.Style.Value == BorderStyleValues.Dotted) style = SpreadsheetBorderStyle.Dotted;
        else if (bpt.Style.Value == BorderStyleValues.Double) style = SpreadsheetBorderStyle.Double;
        else style = SpreadsheetBorderStyle.Thin;

        var color = "#000000";
        if (bpt.Color?.Rgb is not null)
            color = $"#{bpt.Color.Rgb.Value}";
        else if (bpt.Color?.Theme is not null)
            color = ThemeColorToHex(bpt.Color.Theme.Value, bpt.Color.Tint?.Value);

        return new SpreadsheetBorder(style, color);
    }

    private static SpreadsheetHorizontalAlign ParseHorizontalAlign(HorizontalAlignmentValues? value)
    {
        if (value == HorizontalAlignmentValues.Center) return SpreadsheetHorizontalAlign.Center;
        if (value == HorizontalAlignmentValues.Right) return SpreadsheetHorizontalAlign.Right;
        if (value == HorizontalAlignmentValues.Left) return SpreadsheetHorizontalAlign.Left;
        return SpreadsheetHorizontalAlign.General;
    }

    private static SpreadsheetVerticalAlign ParseVerticalAlign(VerticalAlignmentValues? value)
    {
        if (value == VerticalAlignmentValues.Top) return SpreadsheetVerticalAlign.Top;
        if (value == VerticalAlignmentValues.Center) return SpreadsheetVerticalAlign.Middle;
        return SpreadsheetVerticalAlign.Bottom;
    }

    private static string BuiltinNumberFormat(int id) => id switch
    {
        0 => "General",
        1 => "0",
        2 => "0.00",
        3 => "#,##0",
        4 => "#,##0.00",
        9 => "0%",
        10 => "0.00%",
        11 => "0.00E+00",
        12 => "# ?/?",
        13 => "# ??/??",
        14 => "mm-dd-yy",
        15 => "d-mmm-yy",
        16 => "d-mmm",
        17 => "mmm-yy",
        18 => "h:mm AM/PM",
        19 => "h:mm:ss AM/PM",
        20 => "h:mm",
        21 => "h:mm:ss",
        22 => "m/d/yy h:mm",
        37 => "#,##0 ;(#,##0)",
        38 => "#,##0 ;[Red](#,##0)",
        39 => "#,##0.00;(#,##0.00)",
        40 => "#,##0.00;[Red](#,##0.00)",
        45 => "mm:ss",
        46 => "[h]:mm:ss",
        47 => "mmss.0",
        48 => "##0.0E+0",
        49 => "@",
        _ => "General"
    };

    private static string ThemeColorToHex(uint theme, double? tint)
    {
        // Simplified: map common theme colors to hex
        var baseColor = theme switch
        {
            0 => "FFFFFF", // light1
            1 => "000000", // dark1
            2 => "EEECE1", // light2
            3 => "1F497D", // dark2
            4 => "4F81BD", // accent1
            5 => "C0504D", // accent2
            6 => "9BBB59", // accent3
            7 => "8064A2", // accent4
            8 => "4BACC6", // accent5
            9 => "F79646", // accent6
            _ => "000000"
        };
        return $"#{baseColor}";
    }
}
