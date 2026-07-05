using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components.Rendering;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe;

/// <summary>
/// An <see cref="IWireframeComponentProvider"/> that loads component definitions
/// from a JSON string or file content.
///
/// <para>JSON format example:</para>
/// <code>
/// [
///   {
///     "type": "MyCustomCard",
///     "displayName": "Custom Card",
///     "category": "Custom",
///     "icon": "square",
///     "defaultWidth": 200,
///     "defaultHeight": 120,
///     "svgTemplate": "&lt;rect width='{{w}}' height='{{h}}' rx='8' fill='{{props.bgColor}}'/&gt;
///                     &lt;text x='10' y='24'&gt;{{props.title}}&lt;/text&gt;",
///     "props": [
///       { "name": "title",   "displayName": "Title",      "type": "String", "default": "Card Title", "category": "Content"    },
///       { "name": "bgColor", "displayName": "Background", "type": "Color",  "default": "#ffffff",    "category": "Appearance" }
///     ]
///   }
/// ]
/// </code>
///
/// <para>
/// SVG template placeholders:
/// <list type="bullet">
///   <item><c>{{w}}</c> – element width</item>
///   <item><c>{{h}}</c> – element height</item>
///   <item><c>{{props.xxx}}</c> – value of prop named <c>xxx</c></item>
/// </list>
/// </para>
/// </summary>
public sealed class JsonWireframeComponentProvider : IWireframeComponentProvider
{
    private readonly List<WireframeComponentDef> _defs = [];

    /// <inheritdoc/>
    public string ProviderId { get; }

    /// <inheritdoc/>
    public int Priority { get; }

    /// <param name="providerId">Unique provider identifier (e.g. company name).</param>
    /// <param name="priority">Override priority (default 50 – above built-in 0).</param>
    public JsonWireframeComponentProvider(string providerId = "JsonProvider", int priority = 50)
    {
        ProviderId = providerId;
        Priority = priority;
    }

    // ── Loading ───────────────────────────────────────────────────────────────

    /// <summary>Parses component definitions from a JSON string.</summary>
    /// <exception cref="WireframeDeserializationException">On malformed JSON.</exception>
    public void LoadFromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(json);
            root = doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new WireframeDeserializationException("Invalid component definition JSON.", ex);
        }

        var array = root.ValueKind == JsonValueKind.Array
            ? root
            : throw new WireframeDeserializationException("Component definition JSON must be a JSON array.");

        foreach (var item in array.EnumerateArray())
            _defs.Add(ParseDefinition(item));
    }

    /// <summary>Loads definitions from a file on disk.</summary>
    public void LoadFromFile(string filePath)
        => LoadFromJson(File.ReadAllText(filePath));

    /// <inheritdoc/>
    public IEnumerable<WireframeComponentDef> GetDefinitions() => _defs;

    // ── Parsing ───────────────────────────────────────────────────────────────

    private static WireframeComponentDef ParseDefinition(JsonElement el)
    {
        var type = el.GetProperty("type").GetString()
            ?? throw new WireframeDeserializationException("Component definition missing 'type'.");
        var scopeAppId = el.TryGetProperty("scopeAppId", out var scopeProp) ? scopeProp.GetString() : null;
        var localType = el.TryGetProperty("localType", out var localProp) ? localProp.GetString() : null;
        var displayName = el.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? type : type;
        var category = el.TryGetProperty("category", out var cat) ? cat.GetString() ?? "Custom" : "Custom";
        var icon = el.TryGetProperty("icon", out var ic) ? ic.GetString() : null;
        var w = el.TryGetProperty("defaultWidth", out var dw) ? dw.GetDouble() : 160.0;
        var h = el.TryGetProperty("defaultHeight", out var dh) ? dh.GetDouble() : 40.0;
        var isContainer = el.TryGetProperty("isContainer", out var containerProp) && containerProp.GetBoolean();
        var svgTemplate = el.TryGetProperty("svgTemplate", out var tpl) ? tpl.GetString() ?? "" : "";

        var props = new List<PropDef>();
        if (el.TryGetProperty("props", out var propsEl) && propsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in propsEl.EnumerateArray())
                props.Add(ParsePropDef(p));
        }

        return new WireframeComponentDef
        {
            Type = type,
            ScopeAppId = scopeAppId,
            LocalType = localType,
            DisplayName = displayName,
            Category = category,
            Icon = icon,
            DefaultWidth = w,
            DefaultHeight = h,
            Props = props,
            IsBuiltIn = false,
            IsContainer = isContainer,
            RenderSvg = (element, builder) =>
            {
                var markup = ResolvePlaceholders(svgTemplate, element);
                builder.AddMarkupContent(0, markup);
            }
        };
    }

    private static PropDef ParsePropDef(JsonElement el)
    {
        var name = el.GetProperty("name").GetString()
            ?? throw new WireframeDeserializationException("Prop definition missing 'name'.");
        var displayName = el.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? name : name;
        var typeStr = el.TryGetProperty("type", out var t) ? t.GetString() ?? "String" : "String";
        var propType = Enum.TryParse<PropType>(typeStr, ignoreCase: true, out var pt) ? pt : PropType.String;
        var category = el.TryGetProperty("category", out var cat) ? cat.GetString() : null;
        var isRequired = el.TryGetProperty("isRequired", out var req) && req.GetBoolean();

        object? defaultVal = null;
        if (el.TryGetProperty("default", out var def))
        {
            defaultVal = def.ValueKind switch
            {
                JsonValueKind.String => def.GetString(),
                JsonValueKind.Number => def.TryGetInt32(out var i) ? (object)i : def.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        string[]? options = null;
        if (el.TryGetProperty("options", out var opts) && opts.ValueKind == JsonValueKind.Array)
            options = [.. opts.EnumerateArray().Select(o => o.GetString() ?? "")];

        return new PropDef
        {
            Name = name,
            DisplayName = displayName,
            Type = propType,
            Default = defaultVal,
            Options = options,
            Category = category,
            IsRequired = isRequired
        };
    }

    // ── Template resolver ─────────────────────────────────────────────────────

    private static readonly Regex PlaceholderRx = new(@"\{\{(\w+(?:\.\w+)*)\}\}", RegexOptions.Compiled);

    internal static string ResolvePlaceholders(string template, WireframeElement element)
    {
        return PlaceholderRx.Replace(template, match =>
        {
            var key = match.Groups[1].Value;

            if (key == "w") return element.W.ToString("0.##", CultureInfo.InvariantCulture);
            if (key == "h") return element.H.ToString("0.##", CultureInfo.InvariantCulture);
            if (key == "id") return element.Id;
            if (key == "type") return element.Type;

            if (key.StartsWith("props.", StringComparison.Ordinal))
            {
                var propKey = key["props.".Length..];
                return element.Props.GetString(propKey, "");
            }

            return match.Value; // unknown placeholder – leave as-is
        });
    }
}
