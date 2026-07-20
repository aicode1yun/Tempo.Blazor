#pragma warning disable MA0016, MA0051

using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Tempo.Reporting.Abstractions.Definitions.Rdl;

/// <summary>
/// Imports SSRS / Telerik <b>RDL</b> (Report Definition Language, an XML dialect) into the internal
/// <see cref="ReportDefinition"/> model. The importer is <b>namespace-agnostic</b>: RDL elements are matched by
/// their local name, so the SSRS 2008 / 2010 / 2016 schema namespaces (which differ only by year) and
/// Telerik-namespaced RDL all parse where their element vocabulary overlaps.
/// </summary>
/// <remarks>
/// <para>The importer maps the common RDL subset: report/page setup, report parameters, data sources and data
/// sets, and the report items <c>Textbox</c>, <c>Tablix</c>/<c>Table</c>, <c>Chart</c> and <c>Image</c>.</para>
/// <para><b>No silent loss:</b> every RDL construct that is skipped or approximated produces an
/// <see cref="RdlImportDiagnostic"/>. Malformed XML yields an <see cref="RdlDiagnosticSeverity.Error"/> result
/// rather than throwing. Out-of-scope constructs (subreports, custom code, data-region grouping beyond a simple
/// detail row, gauges, maps, matrices with row/column groups, bookmarks, actions) are reported as warnings.</para>
/// </remarks>
public sealed class RdlReportImporter
{
    private static readonly Regex SizeRegex = new(
        @"^\s*(?<value>-?\d*\.?\d+)\s*(?<unit>in|cm|mm|pt|pc|px)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Imports an RDL document supplied as a UTF-8/UTF-16 XML string.</summary>
    /// <param name="rdlXml">The RDL document text.</param>
    /// <returns>The mapped definition and all diagnostics. Check <see cref="RdlImportResult.HasErrors"/> before use.</returns>
    public RdlImportResult Import(string rdlXml)
    {
        if (string.IsNullOrWhiteSpace(rdlXml))
        {
            return Failure("The RDL document is empty.");
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(rdlXml, LoadOptions.None);
        }
        catch (XmlException ex)
        {
            return Failure($"The RDL document is not well-formed XML: {ex.Message}");
        }

        return ImportDocument(document);
    }

    /// <summary>Imports an RDL document from a stream.</summary>
    /// <param name="rdlStream">A readable stream positioned at the start of the RDL document.</param>
    /// <returns>The mapped definition and all diagnostics. Check <see cref="RdlImportResult.HasErrors"/> before use.</returns>
    public RdlImportResult Import(Stream rdlStream)
    {
        ArgumentNullException.ThrowIfNull(rdlStream);

        XDocument document;
        try
        {
            document = XDocument.Load(rdlStream, LoadOptions.None);
        }
        catch (XmlException ex)
        {
            return Failure($"The RDL document is not well-formed XML: {ex.Message}");
        }

        return ImportDocument(document);
    }

    private static RdlImportResult ImportDocument(XDocument document)
    {
        var diagnostics = new List<RdlImportDiagnostic>();
        var root = document.Root;
        if (root is null || !LocalNameIs(root, "Report"))
        {
            return Failure("The RDL root element must be <Report>.");
        }

        var usedIds = new HashSet<string>(StringComparer.Ordinal);

        var name = Value(root, "Name")
            ?? root.Attribute("Name")?.Value
            ?? "Imported RDL report";
        var description = Value(root, "Description");

        var pageSetup = MapPageSetup(root, diagnostics);
        var parameters = MapParameters(root, diagnostics);
        var dataSets = MapDataSets(root, diagnostics);
        WarnDataSourceConnections(root, diagnostics);

        var (body, bodyPath, sectionCount) = ResolveBody(root);
        var elements = MapBody(body, bodyPath, diagnostics, usedIds);
        WarnUnsupportedTopLevel(root, sectionCount, diagnostics);

        var detailHeight = elements.Count == 0
            ? 0
            : elements.Max(element => element.Y + element.Height);

        var definition = new ReportDefinition
        {
            Name = name,
            Description = description,
            PageSetup = pageSetup,
            Parameters = parameters,
            DataSets = dataSets,
            Bands = new ReportBandCollection
            {
                Detail = new ReportBand
                {
                    Kind = ReportBandKind.Detail,
                    Height = detailHeight,
                    Elements = elements,
                },
            },
        };

        return new RdlImportResult(definition, diagnostics);
    }

    // ---- Page setup ---------------------------------------------------------------------------------

    private static ReportPageSetup MapPageSetup(XElement root, List<RdlImportDiagnostic> diagnostics)
    {
        // RDL 2008 places page settings directly under <Report>; RDL 2010/2016 nest them under <Page>.
        // Both are found namespace-agnostically by searching descendants for the well-known local names.
        var width = FirstDescendantValue(root, "PageWidth");
        var height = FirstDescendantValue(root, "PageHeight");

        var pageWidth = TryParseSize(width, out var w, out _) ? w : ReportPageSize.Letter.Width;
        var pageHeight = TryParseSize(height, out var h, out _) ? h : ReportPageSize.Letter.Height;

        if (width is null && height is null)
        {
            diagnostics.Add(Warn("Report/Page", "No PageWidth/PageHeight found; defaulted to US Letter (612x792 pt)."));
        }

        var left = ParseMargin(root, "LeftMargin");
        var right = ParseMargin(root, "RightMargin");
        var top = ParseMargin(root, "TopMargin");
        var bottom = ParseMargin(root, "BottomMargin");

        // Guard against margins that would fail validation (left+right must be < width, top+bottom < height).
        if (left + right >= pageWidth || top + bottom >= pageHeight)
        {
            diagnostics.Add(Warn("Report/Page", "RDL margins do not fit the page; margins reset to 0."));
            left = right = top = bottom = 0;
        }

        return new ReportPageSetup
        {
            PageSize = new ReportPageSize(pageWidth, pageHeight),
            Orientation = pageWidth > pageHeight ? ReportPageOrientation.Landscape : ReportPageOrientation.Portrait,
            Margins = new ReportThickness(left, top, right, bottom),
        };
    }

    private static double ParseMargin(XElement root, string localName)
        => TryParseSize(FirstDescendantValue(root, localName), out var value, out _) ? value : 0;

    // ---- Parameters ---------------------------------------------------------------------------------

    private static List<ReportParameterDefinition> MapParameters(XElement root, List<RdlImportDiagnostic> diagnostics)
    {
        var result = new List<ReportParameterDefinition>();
        var container = Element(root, "ReportParameters");
        if (container is null)
        {
            return result;
        }

        foreach (var parameter in Elements(container, "ReportParameter"))
        {
            var paramName = parameter.Attribute("Name")?.Value ?? Value(parameter, "Name") ?? string.Empty;
            var path = $"Report/ReportParameters/ReportParameter[{paramName}]";
            if (string.IsNullOrWhiteSpace(paramName))
            {
                diagnostics.Add(Warn(path, "ReportParameter without a Name was skipped."));
                continue;
            }

            var dataType = MapParameterType(Value(parameter, "DataType"));
            var prompt = Value(parameter, "Prompt");
            var hidden = ReadBool(Value(parameter, "Hidden"));
            var multiValue = ReadBool(Value(parameter, "MultiValue"));
            var nullable = ReadBool(Value(parameter, "Nullable"));

            string? defaultExpression = null;
            var defaultValue = Element(parameter, "DefaultValue");
            var values = defaultValue is null ? null : Element(defaultValue, "Values");
            var firstValue = values is null ? null : Elements(values, "Value").FirstOrDefault();
            if (firstValue is not null)
            {
                defaultExpression = firstValue.Value;
            }
            else if (defaultValue is not null && Element(defaultValue, "DataSetReference") is not null)
            {
                diagnostics.Add(Warn(path, "DataSet-backed parameter default is not imported; set a default manually."));
            }

            // A hidden parameter is required by the validator to carry a default (it cannot be prompted for).
            // Synthesize an RDL null default rather than dropping the parameter, and record the substitution.
            var synthesizedDefault = false;
            if (hidden && string.IsNullOrWhiteSpace(defaultExpression))
            {
                defaultExpression = "=Nothing";
                synthesizedDefault = true;
                diagnostics.Add(Warn(path, "Hidden parameter had no default; a null default (=Nothing) was supplied."));
            }

            // RDL <Nullable> means "may hold null", which is NOT the same as "optional" — the model has only
            // Required. Treat a parameter as optional when RDL allows null OR a usable default exists, and
            // never emit the contradictory required-with-a-null-default combination from the repair above.
            var required = !nullable && !synthesizedDefault;
            if (nullable || synthesizedDefault)
            {
                diagnostics.Add(Warn(
                    path,
                    synthesizedDefault
                        ? "Parameter marked optional because its default was synthesized; confirm whether a value must be supplied."
                        : "RDL Nullable was mapped to an optional parameter; RDL nullability and optionality are not equivalent."));
            }

            // FluentValidation requires multi-value parameters to be List-typed; align the imported type.
            var dataTypeForModel = multiValue ? ReportParameterType.List : dataType;
            if (multiValue && dataType != ReportParameterType.List)
            {
                diagnostics.Add(Warn(path, "Multi-value parameter was mapped to a List data type."));
            }

            ReportParameterAvailableValues? available = null;
            var validValues = Element(parameter, "ValidValues");
            if (validValues is not null)
            {
                var parameterValues = Element(validValues, "ParameterValues");
                if (parameterValues is not null)
                {
                    var staticValues = Elements(parameterValues, "ParameterValue")
                        .Select(item => new ReportParameterAvailableValue(
                            Value(item, "Value") ?? string.Empty,
                            Value(item, "Label")))
                        .Where(item => !string.IsNullOrEmpty(item.Value))
                        .ToList();
                    if (staticValues.Count > 0)
                    {
                        available = ReportParameterAvailableValues.Static(staticValues);
                    }
                }
                else if (Element(validValues, "DataSetReference") is not null)
                {
                    diagnostics.Add(Warn(path, "DataSet-backed valid values are not imported; only static values are mapped."));
                }
            }

            result.Add(new ReportParameterDefinition
            {
                Name = paramName,
                Label = prompt,
                DataType = dataTypeForModel,
                DefaultExpression = defaultExpression,
                AvailableValues = available,
                AllowMultipleValues = multiValue,
                Hidden = hidden,
                Required = required,
            });
        }

        return result;
    }

    private static ReportParameterType MapParameterType(string? rdlType) => rdlType?.Trim() switch
    {
        "Boolean" => ReportParameterType.Boolean,
        "DateTime" => ReportParameterType.Date,
        "Integer" or "Float" => ReportParameterType.Number,
        _ => ReportParameterType.String,
    };

    // ---- Data sets ----------------------------------------------------------------------------------

    private static List<ReportDataSetDefinition> MapDataSets(XElement root, List<RdlImportDiagnostic> diagnostics)
    {
        var result = new List<ReportDataSetDefinition>();
        var container = Element(root, "DataSets");
        if (container is null)
        {
            return result;
        }

        foreach (var dataSet in Elements(container, "DataSet"))
        {
            var dataSetName = dataSet.Attribute("Name")?.Value ?? Value(dataSet, "Name") ?? string.Empty;
            var path = $"Report/DataSets/DataSet[{dataSetName}]";
            if (string.IsNullOrWhiteSpace(dataSetName))
            {
                diagnostics.Add(Warn(path, "DataSet without a Name was skipped."));
                continue;
            }

            var query = Element(dataSet, "Query");
            var command = query is null ? null : Value(query, "CommandText");
            var sourceName = query is null ? null : Value(query, "DataSourceName");

            var fields = new List<ReportDataSetField>();
            var fieldsElement = Element(dataSet, "Fields");
            if (fieldsElement is not null)
            {
                foreach (var field in Elements(fieldsElement, "Field"))
                {
                    var fieldName = field.Attribute("Name")?.Value ?? Value(field, "Name") ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(fieldName))
                    {
                        diagnostics.Add(Warn($"{path}/Fields/Field", "Field without a Name was skipped."));
                        continue;
                    }

                    var typeName = FirstDescendantValue(field, "TypeName");
                    fields.Add(new ReportDataSetField(fieldName, MapFieldType(typeName)));

                    if (Value(field, "Value") is not null)
                    {
                        diagnostics.Add(Warn(
                            $"{path}/Fields/Field[{fieldName}]",
                            "Calculated field expression is not imported; the field is mapped as a plain column."));
                    }
                }
            }

            if (Element(dataSet, "Filters") is not null)
            {
                diagnostics.Add(Warn($"{path}/Filters", "Data set filters are not imported; filter in the query instead."));
            }

            if (Element(dataSet, "SortExpressions") is not null)
            {
                diagnostics.Add(Warn($"{path}/SortExpressions", "Data set sort expressions are not imported; sort in the query instead."));
            }

            var bindings = new List<ReportDataSetParameterBinding>();
            var queryParameters = query is null ? null : Element(query, "QueryParameters");
            if (queryParameters is not null)
            {
                foreach (var queryParameter in Elements(queryParameters, "QueryParameter"))
                {
                    var qpName = queryParameter.Attribute("Name")?.Value ?? Value(queryParameter, "Name") ?? string.Empty;
                    var qpValue = Value(queryParameter, "Value") ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(qpName))
                    {
                        bindings.Add(new ReportDataSetParameterBinding(qpName, qpValue));
                    }
                }
            }

            result.Add(new ReportDataSetDefinition
            {
                Name = dataSetName,
                Source = string.IsNullOrWhiteSpace(sourceName) ? null : new ReportDataSourceReference { Name = sourceName! },
                Query = command,
                Fields = fields,
                Parameters = bindings,
            });
        }

        return result;
    }

    private static ReportDataFieldType MapFieldType(string? typeName) => typeName?.Trim() switch
    {
        "System.Int16" or "System.Int32" or "System.Int64" or "System.Decimal"
            or "System.Double" or "System.Single" or "System.Byte" => ReportDataFieldType.Number,
        "System.DateTime" or "System.DateTimeOffset" => ReportDataFieldType.Date,
        "System.Boolean" => ReportDataFieldType.Boolean,
        "System.String" or "System.Char" or "System.Guid" => ReportDataFieldType.String,
        null or "" => ReportDataFieldType.String,
        _ => ReportDataFieldType.Object,
    };

    private static void WarnDataSourceConnections(XElement root, List<RdlImportDiagnostic> diagnostics)
    {
        var container = Element(root, "DataSources");
        if (container is null)
        {
            return;
        }

        foreach (var dataSource in Elements(container, "DataSource"))
        {
            var dsName = dataSource.Attribute("Name")?.Value ?? "(unnamed)";
            diagnostics.Add(Warn(
                $"Report/DataSources/DataSource[{dsName}]",
                "Data-source connection details are not imported for security reasons; wire the data source on the server."));
        }
    }

    // ---- Body / report items ------------------------------------------------------------------------

    /// <summary>
    /// Resolves the report body. RDL 2008 puts <c>Body</c> directly under <c>Report</c>; RDL 2010/2016 — the
    /// dominant real-world shape — nest it as <c>Report/ReportSections/ReportSection/Body</c>. The walk is
    /// explicit (never a blind descendant search) so a Body nested inside a data region is never mistaken
    /// for the report body.
    /// </summary>
    private static (XElement? Body, string Path, int SectionCount) ResolveBody(XElement root)
    {
        var direct = Element(root, "Body");
        if (direct is not null)
        {
            return (direct, "Report/Body", 1);
        }

        var sections = Element(root, "ReportSections");
        if (sections is null)
        {
            return (null, "Report/Body", 0);
        }

        var sectionList = Elements(sections, "ReportSection").ToList();
        if (sectionList.Count == 0)
        {
            return (null, "Report/ReportSections", 0);
        }

        return (Element(sectionList[0], "Body"), "Report/ReportSections/ReportSection/Body", sectionList.Count);
    }

    private static List<ReportElement> MapBody(
        XElement? body, string bodyPath, List<RdlImportDiagnostic> diagnostics, HashSet<string> usedIds)
    {
        var elements = new List<ReportElement>();
        var reportItems = body is null ? null : Element(body, "ReportItems");
        if (reportItems is null)
        {
            diagnostics.Add(Warn(bodyPath, "Report body has no ReportItems; the imported report has an empty detail band."));
            return elements;
        }

        foreach (var item in reportItems.Elements())
        {
            var mapped = MapReportItem(item, $"{bodyPath}/ReportItems", diagnostics, usedIds);
            if (mapped is not null)
            {
                elements.Add(mapped);
            }
        }

        return elements;
    }

    private static ReportElement? MapReportItem(
        XElement item,
        string parentPath,
        List<RdlImportDiagnostic> diagnostics,
        HashSet<string> usedIds)
    {
        var localName = item.Name.LocalName;
        var itemName = item.Attribute("Name")?.Value ?? localName;
        var path = $"{parentPath}/{localName}[{itemName}]";

        var mapped = localName switch
        {
            "Textbox" => MapTextbox(item, path, diagnostics, usedIds),
            "Tablix" or "Table" => MapTable(item, path, localName, diagnostics, usedIds),
            "Chart" => MapChart(item, path, diagnostics, usedIds),
            "Image" => MapImage(item, path, diagnostics, usedIds),
            "Rectangle" => MapRectangle(item, path, diagnostics, usedIds),
            "Line" => MapLine(item, path, usedIds),
            "Subreport" => WarnAndSkip(diagnostics, path, "Subreports are not imported."),
            "Matrix" => WarnAndSkip(diagnostics, path, "Matrix data regions (row/column groups) are not imported."),
            "Gauge" or "GaugePanel" => WarnAndSkip(diagnostics, path, "Gauge panels are not imported."),
            "Map" => WarnAndSkip(diagnostics, path, "Map data regions are not imported."),
            "List" => WarnAndSkip(diagnostics, path, "List data regions are not imported."),
            "CustomReportItem" => WarnAndSkip(diagnostics, path, "Custom report items are not imported."),
            _ => WarnAndSkip(diagnostics, path, $"Report item '{localName}' is not supported and was skipped."),
        };

        if (mapped is not null)
        {
            WarnDroppedItemFeatures(item, path, diagnostics);
        }

        return mapped;
    }

    /// <summary>
    /// Reports per-item RDL features that are not carried onto the mapped element. Visibility matters most:
    /// an RDL item with <c>Hidden=true</c> would otherwise render visibly — a silent semantic change.
    /// </summary>
    private static void WarnDroppedItemFeatures(XElement item, string path, List<RdlImportDiagnostic> diagnostics)
    {
        var visibility = Element(item, "Visibility");
        if (visibility is not null)
        {
            var hidden = Value(visibility, "Hidden");
            var toggle = Value(visibility, "ToggleItem");
            if (!string.IsNullOrWhiteSpace(hidden))
            {
                diagnostics.Add(Warn(path, $"Visibility (Hidden='{hidden}') is not imported; the item renders visibly."));
            }

            if (!string.IsNullOrWhiteSpace(toggle))
            {
                diagnostics.Add(Warn(path, "Drill-down toggle visibility is not imported."));
            }
        }

        if (Element(item, "Action") is not null)
        {
            diagnostics.Add(Warn(path, "Item action (hyperlink/bookmark/drill-through) is not imported."));
        }

        if (Element(item, "CustomProperties") is not null)
        {
            diagnostics.Add(Warn(path, "Item custom properties are not imported."));
        }
    }

    private static ReportElement? WarnAndSkip(List<RdlImportDiagnostic> diagnostics, string path, string message)
    {
        diagnostics.Add(Warn(path, message));
        return null;
    }

    private static (double x, double y, double width, double height) MapBounds(
        XElement item, string path, List<RdlImportDiagnostic> diagnostics, double defaultWidth, double defaultHeight)
    {
        var x = TryParseSize(Value(item, "Left"), out var left, out _) ? left : 0;
        var y = TryParseSize(Value(item, "Top"), out var top, out _) ? top : 0;
        var hasWidth = TryParseSize(Value(item, "Width"), out var width, out _);
        var hasHeight = TryParseSize(Value(item, "Height"), out var height, out _);
        if (!hasWidth)
        {
            width = defaultWidth;
        }

        if (!hasHeight)
        {
            height = defaultHeight;
        }

        if (width <= 0 && height <= 0)
        {
            diagnostics.Add(Warn(path, "Report item has no positive size; defaulted so it renders."));
            width = defaultWidth;
            height = defaultHeight;
        }

        return (Math.Max(0, x), Math.Max(0, y), Math.Max(0, width), Math.Max(0, height));
    }

    private static ReportTextBoxElement? MapTextbox(
        XElement item, string path, List<RdlImportDiagnostic> diagnostics, HashSet<string> usedIds)
    {
        var (x, y, width, height) = MapBounds(item, path, diagnostics, 120, 20);
        var (text, expression) = ReadTextboxContent(item);
        if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(expression))
        {
            // The RDL textbox genuinely prints nothing, and the definition model requires non-empty content
            // (ReportTextBox.Content.Required). Skip it with a diagnostic rather than substituting the
            // designer-generated Name, which would INVENT visible text like "Textbox27" in the output.
            diagnostics.Add(Warn(path, "Textbox has no value; the empty text box was skipped."));
            return null;
        }

        var style = ReadTextStyle(item);

        return new ReportTextBoxElement
        {
            Id = UniqueId(item, usedIds),
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Text = expression is null ? text : null,
            Expression = expression,
            TextStyle = style.style,
            HorizontalAlignment = style.horizontal,
            VerticalAlignment = style.vertical,
            TextDirection = style.direction,
            CanGrow = ReadBool(Value(item, "CanGrow")),
        };
    }

    private static ReportElement? MapTable(
        XElement item, string path, string localName, List<RdlImportDiagnostic> diagnostics, HashSet<string> usedIds)
    {
        var (x, y, width, height) = MapBounds(item, path, diagnostics, 400, 60);

        var columnWidths = ReadColumnWidths(item, localName);
        if (columnWidths.Count == 0)
        {
            diagnostics.Add(Warn(path, $"{localName} has no columns; the data region was skipped."));
            return null;
        }

        var rows = ReadTableRows(item, localName);
        var headerTexts = rows.Count > 0 ? rows[0] : [];
        var detailTexts = rows.Count > 1 ? rows[1] : [];

        var columns = new List<ReportTableColumn>();
        for (var index = 0; index < columnWidths.Count; index++)
        {
            var columnHeader = index < headerTexts.Count ? headerTexts[index].text ?? string.Empty : $"Column {index + 1}";
            columns.Add(new ReportTableColumn(columnHeader, columnWidths[index]));
        }

        var header = new ReportTableRow
        {
            Cells = [.. Enumerable.Range(0, columnWidths.Count).Select(index =>
                MakeCell(index < headerTexts.Count ? headerTexts[index] : (null, null)))],
        };
        var detail = new ReportTableRow
        {
            Cells = [.. Enumerable.Range(0, columnWidths.Count).Select(index =>
                MakeCell(index < detailTexts.Count ? detailTexts[index] : (null, null)))],
        };

        if (HasGrouping(item, localName))
        {
            diagnostics.Add(Warn(path, "Row groups beyond a single detail row are not imported; only header and first detail row are mapped."));
        }

        if (HasColumnGrouping(item, localName))
        {
            diagnostics.Add(Warn(path, "Column grouping is not imported; the tablix was flattened to its static columns."));
        }

        if (rows.Count > 2)
        {
            diagnostics.Add(Warn(path, $"{localName} has {rows.Count} rows; only the header and first detail row were mapped."));
        }

        return new ReportTableElement
        {
            Id = UniqueId(item, usedIds),
            X = x,
            Y = y,
            Width = width,
            Height = height,
            DataSetName = Value(item, "DataSetName"),
            Header = header,
            Detail = detail,
            Columns = columns,
        };
    }

    private static ReportTableCell MakeCell((string? text, string? expression) content)
        => new()
        {
            Text = content.expression is null ? content.text : null,
            Expression = content.expression,
        };

    private static ReportElement MapChart(
        XElement item, string path, List<RdlImportDiagnostic> diagnostics, HashSet<string> usedIds)
    {
        var (x, y, width, height) = MapBounds(item, path, diagnostics, 400, 300);

        var categoryExpression = ReadChartCategoryExpression(item);
        if (string.IsNullOrWhiteSpace(categoryExpression))
        {
            categoryExpression = "=Fields!Category.Value";
            diagnostics.Add(Warn(path, "Chart category grouping expression was not found; a placeholder expression was used."));
        }

        var (chartType, seriesSubtype, unmappedType) = ReadChartType(item);
        if (unmappedType is not null)
        {
            diagnostics.Add(Warn(path, $"Chart type '{unmappedType}' has no equivalent and was imported as a column chart."));
        }
        var series = ReadChartSeries(item, categoryExpression, path, diagnostics);
        if (series.Count == 0)
        {
            series.Add(new ReportChartSeries
            {
                Name = "Series1",
                CategoryExpression = categoryExpression,
                ValueExpression = "=Fields!Value.Value",
            });
            diagnostics.Add(Warn(path, "Chart has no series values; a placeholder series was created."));
        }

        var title = ReadChartTitle(item);

        if (!string.IsNullOrEmpty(seriesSubtype)
            && !seriesSubtype.Contains("Stacked", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(seriesSubtype, "Plain", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(Warn(path, $"Chart subtype '{seriesSubtype}' was approximated to the nearest supported chart type."));
        }

        return new ReportChartElement
        {
            Id = UniqueId(item, usedIds),
            X = x,
            Y = y,
            Width = width,
            Height = height,
            ChartType = chartType,
            DataSetName = Value(item, "DataSetName"),
            Title = title,
            Series = series,
        };
    }

    private static ReportElement MapImage(
        XElement item, string path, List<RdlImportDiagnostic> diagnostics, HashSet<string> usedIds)
    {
        var (x, y, width, height) = MapBounds(item, path, diagnostics, 100, 100);
        var source = Value(item, "Source");
        var value = Value(item, "Value") ?? string.Empty;
        var mime = Value(item, "MIMEType");

        var sourceKind = source switch
        {
            "Embedded" => ReportImageSourceKind.Embedded,
            "Database" => ReportImageSourceKind.Expression,
            "External" => ReportImageSourceKind.Url,
            _ => ReportImageSourceKind.Url,
        };

        if (source == "Embedded")
        {
            diagnostics.Add(Warn(path, "Embedded image references the RDL EmbeddedImages catalog by name, which is not imported; supply the image bytes on the server."));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            value = "about:blank";
            diagnostics.Add(Warn(path, "Image has no source value; a placeholder was used."));
        }

        return new ReportImageElement
        {
            Id = UniqueId(item, usedIds),
            X = x,
            Y = y,
            Width = width,
            Height = height,
            SourceKind = sourceKind,
            Source = value,
            ContentType = mime,
        };
    }

    private static ReportElement MapRectangle(
        XElement item, string path, List<RdlImportDiagnostic> diagnostics, HashSet<string> usedIds)
    {
        var (x, y, width, height) = MapBounds(item, path, diagnostics, 100, 40);
        var nested = Element(item, "ReportItems");
        if (nested is not null && nested.Elements().Any())
        {
            diagnostics.Add(Warn(path, "Rectangle contents (nested report items) are not imported; the rectangle is mapped as an empty shape."));
        }

        return new ReportShapeElement
        {
            Id = UniqueId(item, usedIds),
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Shape = ReportShapeKind.Rectangle,
        };
    }

    private static ReportElement MapLine(XElement item, string path, HashSet<string> usedIds)
    {
        var x = TryParseSize(Value(item, "Left"), out var left, out _) ? left : 0;
        var y = TryParseSize(Value(item, "Top"), out var top, out _) ? top : 0;
        var width = TryParseSize(Value(item, "Width"), out var w, out _) ? w : 1;
        var height = TryParseSize(Value(item, "Height"), out var h, out _) ? h : 1;

        return new ReportLineElement
        {
            Id = UniqueId(item, usedIds),
            X = Math.Max(0, x),
            Y = Math.Max(0, y),
            Width = Math.Max(0, width),
            Height = Math.Max(0, height),
        };
    }

    private static void WarnUnsupportedTopLevel(XElement root, int sectionCount, List<RdlImportDiagnostic> diagnostics)
    {
        if (Element(root, "Code") is not null)
        {
            diagnostics.Add(Warn("Report/Code", "Custom code blocks are not imported."));
        }

        if (Element(root, "CodeModules") is not null)
        {
            diagnostics.Add(Warn("Report/CodeModules", "Custom assembly references (CodeModules) are not imported."));
        }

        if (Element(root, "EmbeddedImages") is not null)
        {
            diagnostics.Add(Warn("Report/EmbeddedImages", "Embedded image bytes are not imported; supply them on the server."));
        }

        // Only warn when sections are genuinely dropped: a single-section 2010/2016 report imports fully.
        if (sectionCount > 1)
        {
            diagnostics.Add(Warn(
                "Report/ReportSections",
                $"Report has {sectionCount} sections; only the first section body is imported."));
        }

        // Page headers/footers exist under Report (2008) or ReportSection (2010+); the band model has
        // PageHeader/PageFooter slots but RDL header/footer contents are not mapped in this subset.
        if (FindDescendant(root, "PageHeader") is not null)
        {
            diagnostics.Add(Warn("Report/PageHeader", "Page header contents are not imported."));
        }

        if (FindDescendant(root, "PageFooter") is not null)
        {
            diagnostics.Add(Warn("Report/PageFooter", "Page footer contents are not imported."));
        }

        if (FindDescendant(root, "Variables") is not null)
        {
            diagnostics.Add(Warn("Report/Variables", "Report variables are not imported."));
        }

        if (FindDescendant(root, "CustomProperties") is not null)
        {
            diagnostics.Add(Warn("Report/CustomProperties", "Custom properties are not imported."));
        }
    }

    // ---- Content / style readers --------------------------------------------------------------------

    private static (string? text, string? expression) ReadTextboxContent(XElement textbox)
    {
        // RDL 2005 style: <Textbox><Value>...</Value>. RDL 2008+: Paragraphs/Paragraph/TextRuns/TextRun/Value.
        var directValue = Element(textbox, "Value");
        if (directValue is not null)
        {
            return Classify(directValue.Value);
        }

        var paragraphs = Element(textbox, "Paragraphs");
        if (paragraphs is not null)
        {
            var runValue = paragraphs
                .Descendants()
                .FirstOrDefault(element => LocalNameIs(element, "Value"));
            if (runValue is not null)
            {
                return Classify(runValue.Value);
            }
        }

        return (null, null);
    }

    private static (string? text, string? expression) Classify(string raw)
    {
        if (raw.StartsWith('='))
        {
            return (null, raw);
        }

        return (raw, null);
    }

    private static (ReportTextStyle style, ReportHorizontalAlignment horizontal, ReportVerticalAlignment vertical, ReportTextDirection direction) ReadTextStyle(XElement item)
    {
        var style = new ReportTextStyle();
        var horizontal = ReportHorizontalAlignment.Left;
        var vertical = ReportVerticalAlignment.Top;
        var direction = ReportTextDirection.Auto;

        // RDL nests style across the Textbox <Style> (box), the Paragraph <Style> (alignment) and the
        // TextRun <Style> (font). Read each property from the first descendant with that local name so all
        // three levels are honored regardless of the RDL schema version.
        var fontFamily = FirstDescendantValue(item, "FontFamily");
        var fontSize = FirstDescendantValue(item, "FontSize");
        var fontWeight = FirstDescendantValue(item, "FontWeight");
        var fontStyle = FirstDescendantValue(item, "FontStyle");
        var color = FirstDescendantValue(item, "Color");
        var decoration = FirstDescendantValue(item, "TextDecoration");
        var align = FirstDescendantValue(item, "TextAlign");
        var valign = FirstDescendantValue(item, "VerticalAlign");
        var directionValue = FirstDescendantValue(item, "Direction");

        style = style with
        {
            FontFamily = string.IsNullOrWhiteSpace(fontFamily) ? style.FontFamily : fontFamily!,
            FontSize = TryParseSize(fontSize, out var size, out _) && size > 0 ? size : style.FontSize,
            Bold = fontWeight is not null && fontWeight.Contains("Bold", StringComparison.OrdinalIgnoreCase),
            Italic = string.Equals(fontStyle, "Italic", StringComparison.OrdinalIgnoreCase),
            Underline = decoration is not null && decoration.Contains("Underline", StringComparison.OrdinalIgnoreCase),
            StrikeThrough = decoration is not null && decoration.Contains("LineThrough", StringComparison.OrdinalIgnoreCase),
            Color = string.IsNullOrWhiteSpace(color) ? style.Color : color!,
        };

        horizontal = align switch
        {
            "Center" => ReportHorizontalAlignment.Center,
            "Right" => ReportHorizontalAlignment.Right,
            "Justify" => ReportHorizontalAlignment.Justify,
            _ => ReportHorizontalAlignment.Left,
        };

        vertical = valign switch
        {
            "Middle" => ReportVerticalAlignment.Middle,
            "Bottom" => ReportVerticalAlignment.Bottom,
            _ => ReportVerticalAlignment.Top,
        };

        if (string.Equals(directionValue, "RTL", StringComparison.OrdinalIgnoreCase))
        {
            direction = ReportTextDirection.Rtl;
        }
        else if (string.Equals(directionValue, "LTR", StringComparison.OrdinalIgnoreCase))
        {
            direction = ReportTextDirection.Ltr;
        }

        return (style, horizontal, vertical, direction);
    }

    private static List<double> ReadColumnWidths(XElement item, string localName)
    {
        var widths = new List<double>();
        var columnsContainer = localName == "Tablix"
            ? FindDescendant(item, "TablixColumns")
            : FindDescendant(item, "TableColumns");
        if (columnsContainer is null)
        {
            return widths;
        }

        var columnName = localName == "Tablix" ? "TablixColumn" : "TableColumn";
        foreach (var column in Elements(columnsContainer, columnName))
        {
            widths.Add(TryParseSize(Value(column, "Width"), out var width, out _) ? width : 80);
        }

        return widths;
    }

    private static List<List<(string? text, string? expression)>> ReadTableRows(XElement item, string localName)
    {
        var result = new List<List<(string?, string?)>>();
        if (localName == "Tablix")
        {
            var rowsContainer = FindDescendant(item, "TablixRows");
            if (rowsContainer is not null)
            {
                foreach (var row in Elements(rowsContainer, "TablixRow"))
                {
                    var cellsContainer = Element(row, "TablixCells");
                    if (cellsContainer is null)
                    {
                        continue;
                    }

                    var cells = new List<(string?, string?)>();
                    foreach (var cell in Elements(cellsContainer, "TablixCell"))
                    {
                        var contents = Element(cell, "CellContents");
                        var textbox = contents is null ? null : FindDescendantElement(contents, "Textbox");
                        cells.Add(textbox is null ? (null, null) : ReadTextboxContent(textbox));
                    }

                    result.Add(cells);
                }
            }

            return result;
        }

        // Legacy <Table>: header rows then detail rows.
        foreach (var section in new[] { "Header", "Details", "Footer" })
        {
            var sectionElement = FindDescendant(item, section);
            var rowsContainer = sectionElement is null ? null : Element(sectionElement, "TableRows");
            if (rowsContainer is null)
            {
                continue;
            }

            foreach (var row in Elements(rowsContainer, "TableRow"))
            {
                var cellsContainer = Element(row, "TableCells");
                if (cellsContainer is null)
                {
                    continue;
                }

                var cells = new List<(string?, string?)>();
                foreach (var cell in Elements(cellsContainer, "TableCell"))
                {
                    var textbox = FindDescendantElement(cell, "Textbox");
                    cells.Add(textbox is null ? (null, null) : ReadTextboxContent(textbox));
                }

                result.Add(cells);
            }
        }

        return result;
    }

    /// <summary>Detects row grouping. Column grouping is reported separately by <see cref="HasColumnGrouping"/>.</summary>
    private static bool HasGrouping(XElement item, string localName)
    {
        if (localName == "Tablix")
        {
            var rowHierarchy = FindDescendant(item, "TablixRowHierarchy");
            if (rowHierarchy is not null)
            {
                return rowHierarchy.Descendants().Any(element => LocalNameIs(element, "Group"));
            }

            return false;
        }

        return item.Descendants().Any(element => LocalNameIs(element, "TableGroups") || LocalNameIs(element, "Grouping"));
    }

    /// <summary>
    /// Detects tablix COLUMN grouping (a pivoted/matrix-style column hierarchy). Such a tablix is flattened
    /// to its static columns during import, so the caller must diagnose it.
    /// </summary>
    private static bool HasColumnGrouping(XElement item, string localName)
    {
        if (localName != "Tablix")
        {
            return false;
        }

        var columnHierarchy = FindDescendant(item, "TablixColumnHierarchy");
        return columnHierarchy is not null
            && columnHierarchy.Descendants().Any(element => LocalNameIs(element, "Group"));
    }

    private static string? ReadChartCategoryExpression(XElement chart)
    {
        var categoryHierarchy = FindDescendant(chart, "ChartCategoryHierarchy");
        var groupExpression = categoryHierarchy?
            .Descendants()
            .FirstOrDefault(element => LocalNameIs(element, "GroupExpression"));
        return groupExpression?.Value;
    }

    private static (ReportChartType type, string? subtype, string? unmappedType) ReadChartType(XElement chart)
    {
        // RDL 2008: type + subtype on the ChartSeries (<Type>Column</Type><Subtype>Stacked</Subtype>).
        var firstSeries = FindDescendant(chart, "ChartSeries");
        var typeName = firstSeries is null ? null : Value(firstSeries, "Type");
        var subtype = firstSeries is null ? null : Value(firstSeries, "Subtype");

        // RDL 2005 fallback: <Type> directly on the Chart (e.g. "Column", "Pie", "Bar").
        typeName ??= Value(chart, "Type");

        var stacked = subtype is not null && subtype.Contains("Stacked", StringComparison.OrdinalIgnoreCase);

        var trimmedType = typeName?.Trim();
        var type = trimmedType switch
        {
            "Bar" => stacked ? ReportChartType.StackedBar : ReportChartType.Bar,
            "Line" or "Smooth" => ReportChartType.Line,
            "Area" => stacked ? ReportChartType.StackedArea : ReportChartType.Area,
            "Shape" => subtype is not null && subtype.Contains("Doughnut", StringComparison.OrdinalIgnoreCase)
                ? ReportChartType.Donut
                : ReportChartType.Pie,
            "Pie" => ReportChartType.Pie,
            "Doughnut" => ReportChartType.Donut,
            "Column" => stacked ? ReportChartType.StackedColumn : ReportChartType.Column,
            _ => stacked ? ReportChartType.StackedColumn : ReportChartType.Column,
        };

        // Anything that fell through the switch (Scatter, Range, Polar, Funnel…) was silently turned into a
        // column chart; hand the original name back so the caller can diagnose the substitution.
        var recognized = trimmedType is "Bar" or "Line" or "Smooth" or "Area" or "Shape"
            or "Pie" or "Doughnut" or "Column";
        return (type, subtype, recognized ? null : trimmedType);
    }

    private static List<ReportChartSeries> ReadChartSeries(
        XElement chart, string categoryExpression, string path, List<RdlImportDiagnostic> diagnostics)
    {
        var result = new List<ReportChartSeries>();
        var seriesCollection = FindDescendant(chart, "ChartSeriesCollection");
        if (seriesCollection is null)
        {
            return result;
        }

        var index = 0;
        foreach (var series in Elements(seriesCollection, "ChartSeries"))
        {
            index++;
            var seriesName = series.Attribute("Name")?.Value ?? $"Series{index}";
            var valueExpression = series
                .Descendants()
                .FirstOrDefault(element => LocalNameIs(element, "Y"))?.Value;

            if (string.IsNullOrWhiteSpace(valueExpression))
            {
                valueExpression = "=Fields!Value.Value";
                diagnostics.Add(Warn($"{path}/ChartSeries[{seriesName}]", "Series has no Y value expression; a placeholder was used."));
            }

            result.Add(new ReportChartSeries
            {
                Name = seriesName,
                CategoryExpression = categoryExpression,
                ValueExpression = valueExpression!,
            });
        }

        return result;
    }

    private static string? ReadChartTitle(XElement chart)
    {
        var titles = FindDescendant(chart, "Titles");
        var caption = titles?
            .Descendants()
            .FirstOrDefault(element => LocalNameIs(element, "Caption"));
        return caption?.Value;
    }

    // ---- Low-level helpers --------------------------------------------------------------------------

    private static string UniqueId(XElement item, HashSet<string> usedIds)
    {
        var baseId = item.Attribute("Name")?.Value;
        if (string.IsNullOrWhiteSpace(baseId))
        {
            baseId = item.Name.LocalName;
        }

        var candidate = baseId!;
        var suffix = 1;
        while (!usedIds.Add(candidate))
        {
            candidate = $"{baseId}_{++suffix}";
        }

        return candidate;
    }

    private static bool LocalNameIs(XElement element, string localName)
        => string.Equals(element.Name.LocalName, localName, StringComparison.Ordinal);

    private static XElement? Element(XElement parent, string localName)
        => parent.Elements().FirstOrDefault(element => LocalNameIs(element, localName));

    private static IEnumerable<XElement> Elements(XElement parent, string localName)
        => parent.Elements().Where(element => LocalNameIs(element, localName));

    private static string? Value(XElement parent, string localName)
        => Element(parent, localName)?.Value;

    private static XElement? FindDescendant(XElement parent, string localName)
        => parent.Descendants().FirstOrDefault(element => LocalNameIs(element, localName));

    private static XElement? FindDescendantElement(XElement parent, string localName)
        => parent.Descendants().FirstOrDefault(element => LocalNameIs(element, localName));

    private static string? FirstDescendantValue(XElement root, string localName)
        => root.Descendants().FirstOrDefault(element => LocalNameIs(element, localName))?.Value;

    private static bool ReadBool(string? value)
        => string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseSize(string? raw, out double points, out bool hadUnit)
    {
        points = 0;
        hadUnit = false;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var match = SizeRegex.Match(raw);
        if (!match.Success)
        {
            return false;
        }

        var number = double.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
        var unit = match.Groups["unit"].Value.ToLowerInvariant();
        hadUnit = unit.Length > 0;
        points = unit switch
        {
            "in" => number * 72.0,
            "cm" => number * 28.3464566929,
            "mm" => number * 2.8346456693,
            "pc" => number * 12.0,
            "px" => number * 0.75,
            "pt" or "" => number,
            _ => number,
        };

        return true;
    }

    private static RdlImportDiagnostic Warn(string path, string message)
        => new(RdlDiagnosticSeverity.Warning, path, message);

    private static RdlImportResult Failure(string message)
    {
        // A minimal, VALID placeholder so callers can inspect diagnostics without a null definition. It is
        // never persisted because HasErrors is true.
        var definition = new ReportDefinition
        {
            Name = "Invalid RDL import",
            Bands = new ReportBandCollection { Detail = new ReportBand { Kind = ReportBandKind.Detail } },
        };
        return new RdlImportResult(definition, [new RdlImportDiagnostic(RdlDiagnosticSeverity.Error, "Report", message)]);
    }
}

#pragma warning restore MA0016, MA0051
