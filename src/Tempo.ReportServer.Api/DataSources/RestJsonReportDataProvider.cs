using System.Text.Json;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;

namespace Tempo.ReportServer.Api.DataSources;

/// <summary>REST/JSON implementation of <see cref="IReportDataProvider"/>.</summary>
public sealed class RestJsonReportDataProvider : IReportDataProvider
{
    private readonly IReportDataSourceRegistry _registry;
    private readonly HttpClient _httpClient;

    /// <summary>Creates a REST/JSON provider.</summary>
    public RestJsonReportDataProvider(IReportDataSourceRegistry registry, HttpClient httpClient)
    {
        _registry = registry;
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<ReportDataSetResult> GetDataAsync(
        string dataSetName,
        ReportDataQuery query,
        IReadOnlyDictionary<string, ReportParameterValue> parameters,
        ReportExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var source = ResolveSource(query, context);
        var relativeUrl = ExpandUrlTemplate(query.Text ?? string.Empty, parameters);
        var uri = new Uri(source.Rest!.BaseUri, relativeUrl);

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        foreach (var header in source.Rest.Headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        using var response = await _httpClient.SendAsync(request, context.CancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ReportDataProviderException(
                "RestJson.HttpError",
                $"REST data source returned HTTP {(int)response.StatusCode}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(context.CancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: context.CancellationToken);
        var selected = SelectJson(document.RootElement, query.Selector);
        var rows = MaterializeRows(selected, query.MaxRows);
        var schema = InferSchema(rows);
        return new ReportDataSetResult(schema, StreamRows(rows, context.CancellationToken));
    }

    private NamedReportDataSource ResolveSource(ReportDataQuery query, ReportExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(query.SourceName))
        {
            throw new ReportDataProviderException("RestJson.SourceRequired", "REST JSON query requires a named data source.");
        }

        var source = _registry.Resolve(query.SourceName, context);
        if (source?.Rest is null)
        {
            throw new ReportDataProviderException(
                "RestJson.SourceNotFound",
                $"REST JSON data source '{query.SourceName}' was not found for tenant '{context.TenantId}'.");
        }

        return source;
    }

    private static string ExpandUrlTemplate(
        string template,
        IReadOnlyDictionary<string, ReportParameterValue> parameters)
    {
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < template.Length; i++)
        {
            if (template[i] != '{')
            {
                builder.Append(template[i]);
                continue;
            }

            var end = template.IndexOf('}', i + 1);
            if (end < 0)
            {
                throw new ReportDataProviderException("RestJson.InvalidTemplate", "URL template contains an unclosed parameter.");
            }

            var name = template.Substring(i + 1, end - i - 1);
            if (!parameters.TryGetValue(name, out var value))
            {
                throw new ReportDataProviderException(
                    "RestJson.UnknownTemplateParameter",
                    $"URL template parameter '{name}' is not a report parameter.");
            }

            var joined = string.Join(",", value.Values.Select(v => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)));
            builder.Append(Uri.EscapeDataString(joined));
            i = end;
        }

        return builder.ToString();
    }

    private static JsonElement SelectJson(JsonElement root, string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector) || selector == "$")
        {
            return root.Clone();
        }

        if (selector.StartsWith("/", StringComparison.Ordinal))
        {
            return SelectJsonPointer(root, selector).Clone();
        }

        if (selector.StartsWith("$.", StringComparison.Ordinal))
        {
            var current = root;
            foreach (var segment in selector[2..].Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                {
                    throw new ReportDataProviderException("RestJson.SelectorNotFound", $"JSONPath selector '{selector}' was not found.");
                }
            }

            return current.Clone();
        }

        throw new ReportDataProviderException("RestJson.UnsupportedSelector", $"Selector '{selector}' is not supported.");
    }

    private static JsonElement SelectJsonPointer(JsonElement root, string pointer)
    {
        var current = root;
        foreach (var rawSegment in pointer.Split('/').Skip(1))
        {
            var segment = rawSegment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            if (current.ValueKind == JsonValueKind.Object)
            {
                if (!current.TryGetProperty(segment, out current))
                {
                    throw new ReportDataProviderException("RestJson.SelectorNotFound", $"JSON Pointer '{pointer}' was not found.");
                }
            }
            else if (current.ValueKind == JsonValueKind.Array && int.TryParse(segment, out var index) && index >= 0 && index < current.GetArrayLength())
            {
                current = current.EnumerateArray().ElementAt(index);
            }
            else
            {
                throw new ReportDataProviderException("RestJson.SelectorNotFound", $"JSON Pointer '{pointer}' was not found.");
            }
        }

        return current;
    }

    private static List<ReportDataRow> MaterializeRows(JsonElement selected, int? maxRows)
    {
        var rows = selected.ValueKind == JsonValueKind.Array
            ? selected.EnumerateArray().Select(ElementToRow)
            : [ElementToRow(selected)];

        return maxRows is > -1 ? rows.Take(maxRows.Value).ToList() : rows.ToList();
    }

    private static ReportDataRow ElementToRow(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            return new ReportDataRow(element.EnumerateObject()
                .ToDictionary(property => property.Name, property => JsonToObject(property.Value), StringComparer.Ordinal));
        }

        return new ReportDataRow(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["value"] = JsonToObject(element),
        });
    }

    private static object? JsonToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.False => false,
            JsonValueKind.True => true,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => element.GetDouble(),
            _ => element.GetRawText(),
        };
    }

    private static IReadOnlyList<ReportDataColumn> InferSchema(IReadOnlyList<ReportDataRow> rows)
        => rows.FirstOrDefault()?.Values
            .Select(pair => new ReportDataColumn(pair.Key, InferType(pair.Value)))
            .ToArray() ?? [];

    private static async IAsyncEnumerable<ReportDataRow> StreamRows(
        IEnumerable<ReportDataRow> rows,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return row;
        }
    }

    private static ReportDataFieldType InferType(object? value)
    {
        if (value is null)
        {
            return ReportDataFieldType.Object;
        }

        return value switch
        {
            string => ReportDataFieldType.String,
            bool => ReportDataFieldType.Boolean,
            DateTime or DateTimeOffset => ReportDataFieldType.Date,
            byte or short or int or long or float or double or decimal => ReportDataFieldType.Number,
            _ => ReportDataFieldType.Object,
        };
    }
}
