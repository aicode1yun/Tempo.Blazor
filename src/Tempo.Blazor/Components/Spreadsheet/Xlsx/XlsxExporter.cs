using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Xlsx;

/// <summary>Exports a <see cref="SpreadsheetWorkbook"/> to an XLSX file.</summary>
public static class XlsxExporter
{
    /// <summary>Exports the given workbook to a byte array representing an XLSX file.</summary>
    public static byte[] Export(SpreadsheetWorkbook workbook)
    {
        using var stream = new MemoryStream();
        using var doc = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook);
        var workbookPart = doc.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();

        var sharedStrings = new SharedStringTable();
        var sstPart = workbookPart.AddNewPart<SharedStringTablePart>();
        sstPart.SharedStringTable = sharedStrings;

        var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
        var stylesheet = new Stylesheet();
        stylesPart.Stylesheet = stylesheet;

        // Build style parts
        var fonts = new Fonts();
        var fills = new Fills();
        var borders = new Borders();
        var cellFormats = new CellFormats();
        var numFmts = new NumberingFormats();

        // Default fill (index 0) required by OpenXml
        fills.Append(new Fill { PatternFill = new PatternFill { PatternType = PatternValues.None } });
        // Default fill (index 1) required by OpenXml
        fills.Append(new Fill { PatternFill = new PatternFill { PatternType = PatternValues.Gray125 } });

        var defaultFormat = new CellFormat
        {
            FontId = 0,
            FillId = 0,
            BorderId = 0,
            NumberFormatId = 0,
            FormatId = 0
        };
        cellFormats.Append(defaultFormat);

        // Default font
        fonts.Append(CreateFont(new SpreadsheetCellStyle()));
        // Default border
        borders.Append(new Border());

        var styleIndices = new Dictionary<string, uint>();
        styleIndices[StyleKey(new SpreadsheetCellStyle())] = 0;

        uint numFmtIdCounter = 164; // custom formats start here

        foreach (var sheet in workbook.Sheets)
        {
            foreach (var cell in sheet.Cells.Values)
            {
                var key = StyleKey(cell.Style);
                if (styleIndices.ContainsKey(key)) continue;

                var font = CreateFont(cell.Style);
                var fontId = (uint)fonts.ChildElements.Count;
                fonts.Append(font);

                var fill = CreateFill(cell.Style);
                var fillId = (uint)fills.ChildElements.Count;
                fills.Append(fill);

                var border = CreateBorder(cell.Style);
                var borderId = (uint)borders.ChildElements.Count;
                borders.Append(border);

                uint nfId = 0;
                var nf = cell.Style.NumberFormat;
                if (!string.IsNullOrEmpty(nf) && nf != "General")
                {
                    var builtin = BuiltinNumberFormatId(nf);
                    if (builtin is not null)
                    {
                        nfId = builtin.Value;
                    }
                    else
                    {
                        nfId = numFmtIdCounter++;
                        numFmts.Append(new NumberingFormat { NumberFormatId = nfId, FormatCode = nf });
                    }
                }

                var cf = new CellFormat
                {
                    FontId = fontId,
                    FillId = fillId,
                    BorderId = borderId,
                    NumberFormatId = nfId,
                    FormatId = 0,
                    ApplyFont = true,
                    ApplyFill = true,
                    ApplyBorder = true,
                    ApplyNumberFormat = true,
                    ApplyAlignment = true,
                    Alignment = new Alignment
                    {
                        Horizontal = MapHorizontalAlign(cell.Style.HorizontalAlign),
                        Vertical = MapVerticalAlign(cell.Style.VerticalAlign),
                        WrapText = cell.Style.TextWrap
                    }
                };
                var cfId = (uint)cellFormats.ChildElements.Count;
                cellFormats.Append(cf);
                styleIndices[key] = cfId;
            }
        }

        if (numFmts.ChildElements.Count > 0)
            stylesheet.InsertAt(numFmts, 0);
        stylesheet.Append(fonts);
        stylesheet.Append(fills);
        stylesheet.Append(borders);
        stylesheet.Append(cellFormats);

        var sheetsElement = new Sheets();
        uint sheetId = 1;

        foreach (var sheet in workbook.Sheets)
        {
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var worksheet = new Worksheet();

            // Columns
            if (sheet.Columns.Count > 0)
            {
                var cols = new Columns();
                foreach (var kv in sheet.Columns.OrderBy(c => c.Key))
                {
                    var col = kv.Value;
                    if (col.Width is null && !col.IsHidden) continue;
                    var xlsxCol = new Column
                    {
                        Min = (uint)(col.Index + 1),
                        Max = (uint)(col.Index + 1),
                    };
                    if (col.Width is not null) { xlsxCol.Width = col.Width.Value / 7.0; xlsxCol.CustomWidth = true; }
                    if (col.IsHidden) xlsxCol.Hidden = true;
                    cols.Append(xlsxCol);
                }
                if (cols.ChildElements.Count > 0)
                    worksheet.Append(cols);
            }

            var sheetData = new SheetData();

            // Group cells by row
            var rows = sheet.Cells.GroupBy(c =>
            {
                var parts = new string(c.Key.SkipWhile(char.IsLetter).ToArray());
                return int.TryParse(parts, out var r) ? r : 0;
            }).OrderBy(g => g.Key);

            foreach (var rowGroup in rows)
            {
                var rowIndex = rowGroup.Key;
                var row = new Row { RowIndex = (uint)rowIndex };

                if (sheet.Rows.TryGetValue(rowIndex - 1, out var rowMeta))
                {
                    if (rowMeta.Height is not null) { row.Height = rowMeta.Height.Value; row.CustomHeight = true; }
                    if (rowMeta.IsHidden) row.Hidden = true;
                }

                foreach (var kv in rowGroup.OrderBy(c => c.Key))
                {
                    var cellRef = kv.Key;
                    var cell = kv.Value;
                    var xlsxCell = new Cell { CellReference = cellRef };

                    if (!string.IsNullOrEmpty(cell.Formula))
                    {
                        xlsxCell.CellFormula = new CellFormula(cell.Formula);
                        xlsxCell.CellValue = new CellValue(cell.Value?.ToString() ?? "");
                        xlsxCell.DataType = CellValues.Number;
                    }
                    else if (cell.Value is double d)
                    {
                        xlsxCell.CellValue = new CellValue(d);
                        xlsxCell.DataType = CellValues.Number;
                    }
                    else if (cell.Value is bool b)
                    {
                        xlsxCell.CellValue = new CellValue(b ? "1" : "0");
                        xlsxCell.DataType = CellValues.Boolean;
                    }
                    else if (cell.Value is DateTime dt)
                    {
                        xlsxCell.CellValue = new CellValue(dt.ToOADate());
                        xlsxCell.DataType = CellValues.Number;
                    }
                    else
                    {
                        var text = cell.Value?.ToString() ?? "";
                        var ssi = GetSharedStringIndex(sharedStrings, text);
                        xlsxCell.CellValue = new CellValue(ssi.ToString(CultureInfo.InvariantCulture));
                        xlsxCell.DataType = CellValues.SharedString;
                    }

                    var styleKey = StyleKey(cell.Style);
                    if (styleIndices.TryGetValue(styleKey, out var sidx))
                        xlsxCell.StyleIndex = sidx;

                    row.Append(xlsxCell);
                }

                sheetData.Append(row);
            }

            worksheet.Append(sheetData);

            // Auto-filter definition (must precede mergeCells per the CT_Worksheet schema).
            if (sheet.AutoFilter is not null)
            {
                worksheet.Append(new AutoFilter { Reference = sheet.AutoFilter.Range.ToString() });
            }

            // Merged cells
            if (sheet.MergedCells.Count > 0)
            {
                var mergeCells = new MergeCells();
                foreach (var range in sheet.MergedCells)
                {
                    mergeCells.Append(new MergeCell { Reference = range.ToString() });
                }
                worksheet.Append(mergeCells);
            }

            // Data validation rules
            if (sheet.DataValidations.Count > 0)
            {
                var dvs = new DataValidations();
                foreach (var rule in sheet.DataValidations)
                {
                    var dv = new DataValidation
                    {
                        SequenceOfReferences = new ListValue<StringValue> { InnerText = rule.Range.ToString() },
                        Type = ExportValidationType(rule.Type),
                        Operator = ExportValidationOperator(rule.Operator),
                        AllowBlank = rule.AllowBlank ? true : null,
                        ShowDropDown = rule.ShowDropDown ? null : (bool?)true
                    };
                    if (rule.Formula1 is not null) dv.Append(new Formula1 { Text = rule.Formula1 });
                    if (rule.Formula2 is not null) dv.Append(new Formula2 { Text = rule.Formula2 });
                    if (rule.InputMessage is not null)
                    {
                        dv.ShowInputMessage = true;
                        if (rule.InputMessage.Title is not null) dv.PromptTitle = rule.InputMessage.Title;
                        if (rule.InputMessage.Message is not null) dv.Prompt = rule.InputMessage.Message;
                    }
                    if (rule.ErrorAlert is not null)
                    {
                        dv.ShowErrorMessage = true;
                        dv.ErrorStyle = ExportErrorStyle(rule.ErrorAlert.Style);
                        if (rule.ErrorAlert.Title is not null) dv.ErrorTitle = rule.ErrorAlert.Title;
                        if (rule.ErrorAlert.Message is not null) dv.Error = rule.ErrorAlert.Message;
                    }
                    dvs.Append(dv);
                }
                worksheet.Append(dvs);
            }

            worksheetPart.Worksheet = worksheet;

            sheetsElement.Append(new Sheet
            {
                Name = sheet.Name,
                SheetId = sheetId,
                Id = workbookPart.GetIdOfPart(worksheetPart)
            });
            sheetId++;
        }

        workbookPart.Workbook.Append(sheetsElement);
        workbookPart.Workbook.Save();

        sstPart.SharedStringTable.Save();

        doc.Save();
        stream.Position = 0;
        return stream.ToArray();
    }

    private static int GetSharedStringIndex(SharedStringTable sst, string text)
    {
        for (int i = 0; i < sst.ChildElements.Count; i++)
        {
            if (sst.ChildElements[i].InnerText == text)
                return i;
        }
        sst.Append(new SharedStringItem(new Text(text)));
        return sst.ChildElements.Count - 1;
    }

    private static Font CreateFont(SpreadsheetCellStyle style)
    {
        var font = new Font();
        if (style.Bold) font.Append(new Bold());
        if (style.Italic) font.Append(new Italic());
        if (style.Underline) font.Append(new Underline());
        font.Append(new FontSize { Val = style.FontSize });
        font.Append(new Color { Rgb = new HexBinaryValue(style.ForeColor.TrimStart('#').PadLeft(6, '0')) });
        font.Append(new FontName { Val = style.FontFamily });
        return font;
    }

    private static Fill CreateFill(SpreadsheetCellStyle style)
    {
        if (string.IsNullOrEmpty(style.BackgroundColor) || style.BackgroundColor == "transparent")
        {
            return new Fill { PatternFill = new PatternFill { PatternType = PatternValues.None } };
        }
        return new Fill
        {
            PatternFill = new PatternFill
            {
                PatternType = PatternValues.Solid,
                ForegroundColor = new ForegroundColor
                {
                    Rgb = new HexBinaryValue(style.BackgroundColor.TrimStart('#').PadLeft(6, '0'))
                }
            }
        };
    }

    private static Border CreateBorder(SpreadsheetCellStyle style)
    {
        var border = new Border();
        if (style.BorderTop.Style != SpreadsheetBorderStyle.None)
            border.TopBorder = (TopBorder)MapBorderEdge(style.BorderTop);
        if (style.BorderRight.Style != SpreadsheetBorderStyle.None)
            border.RightBorder = (RightBorder)MapBorderEdge(style.BorderRight);
        if (style.BorderBottom.Style != SpreadsheetBorderStyle.None)
            border.BottomBorder = (BottomBorder)MapBorderEdge(style.BorderBottom);
        if (style.BorderLeft.Style != SpreadsheetBorderStyle.None)
            border.LeftBorder = (LeftBorder)MapBorderEdge(style.BorderLeft);
        return border;
    }

    private static BorderPropertiesType MapBorderEdge(SpreadsheetBorder border)
    {
        var edge = new TopBorder();
        edge.Style = border.Style switch
        {
            SpreadsheetBorderStyle.Thin => BorderStyleValues.Thin,
            SpreadsheetBorderStyle.Medium => BorderStyleValues.Medium,
            SpreadsheetBorderStyle.Thick => BorderStyleValues.Thick,
            SpreadsheetBorderStyle.Dashed => BorderStyleValues.Dashed,
            SpreadsheetBorderStyle.Dotted => BorderStyleValues.Dotted,
            SpreadsheetBorderStyle.Double => BorderStyleValues.Double,
            _ => BorderStyleValues.Thin
        };
        edge.Color = new Color { Rgb = new HexBinaryValue(border.Color.TrimStart('#').PadLeft(6, '0')) };
        return edge;
    }

    private static HorizontalAlignmentValues? MapHorizontalAlign(SpreadsheetHorizontalAlign align) => align switch
    {
        SpreadsheetHorizontalAlign.Left => HorizontalAlignmentValues.Left,
        SpreadsheetHorizontalAlign.Center => HorizontalAlignmentValues.Center,
        SpreadsheetHorizontalAlign.Right => HorizontalAlignmentValues.Right,
        SpreadsheetHorizontalAlign.Justify => HorizontalAlignmentValues.Justify,
        _ => null
    };

    private static VerticalAlignmentValues? MapVerticalAlign(SpreadsheetVerticalAlign align) => align switch
    {
        SpreadsheetVerticalAlign.Top => VerticalAlignmentValues.Top,
        SpreadsheetVerticalAlign.Middle => VerticalAlignmentValues.Center,
        _ => null
    };

    private static string StyleKey(SpreadsheetCellStyle style)
    {
        return $"{style.FontFamily}|{style.FontSize}|{style.Bold}|{style.Italic}|{style.Underline}|{style.ForeColor}|{style.BackgroundColor}|{style.HorizontalAlign}|{style.VerticalAlign}|{style.TextWrap}|{style.NumberFormat}|{style.BorderTop.Style}|{style.BorderTop.Color}|{style.BorderRight.Style}|{style.BorderRight.Color}|{style.BorderBottom.Style}|{style.BorderBottom.Color}|{style.BorderLeft.Style}|{style.BorderLeft.Color}";
    }

    private static uint? BuiltinNumberFormatId(string format) => format switch
    {
        "General" => 0,
        "0" => 1,
        "0.00" => 2,
        "#,##0" => 3,
        "#,##0.00" => 4,
        "0%" => 9,
        "0.00%" => 10,
        "0.00E+00" => 11,
        "# ?/?" => 12,
        "# ??/??" => 13,
        "mm-dd-yy" => 14,
        "d-mmm-yy" => 15,
        "d-mmm" => 16,
        "mmm-yy" => 17,
        "h:mm AM/PM" => 18,
        "h:mm:ss AM/PM" => 19,
        "h:mm" => 20,
        "h:mm:ss" => 21,
        "m/d/yy h:mm" => 22,
        _ => null
    };

    private static DataValidationValues ExportValidationType(SpreadsheetValidationType t)
    {
        if (t == SpreadsheetValidationType.Whole) return DataValidationValues.Whole;
        if (t == SpreadsheetValidationType.Decimal) return DataValidationValues.Decimal;
        if (t == SpreadsheetValidationType.List) return DataValidationValues.List;
        if (t == SpreadsheetValidationType.Date) return DataValidationValues.Date;
        if (t == SpreadsheetValidationType.Time) return DataValidationValues.Time;
        if (t == SpreadsheetValidationType.TextLength) return DataValidationValues.TextLength;
        if (t == SpreadsheetValidationType.Custom) return DataValidationValues.Custom;
        return DataValidationValues.None;
    }

    private static DataValidationOperatorValues ExportValidationOperator(SpreadsheetValidationOperator op)
    {
        if (op == SpreadsheetValidationOperator.NotBetween) return DataValidationOperatorValues.NotBetween;
        if (op == SpreadsheetValidationOperator.Equal) return DataValidationOperatorValues.Equal;
        if (op == SpreadsheetValidationOperator.NotEqual) return DataValidationOperatorValues.NotEqual;
        if (op == SpreadsheetValidationOperator.GreaterThan) return DataValidationOperatorValues.GreaterThan;
        if (op == SpreadsheetValidationOperator.LessThan) return DataValidationOperatorValues.LessThan;
        if (op == SpreadsheetValidationOperator.GreaterOrEqual) return DataValidationOperatorValues.GreaterThanOrEqual;
        if (op == SpreadsheetValidationOperator.LessOrEqual) return DataValidationOperatorValues.LessThanOrEqual;
        return DataValidationOperatorValues.Between;
    }

    private static DataValidationErrorStyleValues ExportErrorStyle(SpreadsheetValidationErrorStyle s)
    {
        if (s == SpreadsheetValidationErrorStyle.Warning) return DataValidationErrorStyleValues.Warning;
        if (s == SpreadsheetValidationErrorStyle.Information) return DataValidationErrorStyleValues.Information;
        return DataValidationErrorStyleValues.Stop;
    }
}
