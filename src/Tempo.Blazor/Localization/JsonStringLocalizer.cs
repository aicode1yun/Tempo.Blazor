using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Localization;

namespace Tempo.Blazor.Localization;

/// <summary>
/// An <see cref="IStringLocalizer{TResourceSource}"/> backed by JSON resources embedded in the
/// resource type's assembly: <c>{TResourceSource.FullName}.json</c> (neutral) and
/// <c>{TResourceSource.FullName}.{culture}.json</c> (per culture), each a flat
/// <c>{ "Key": "Value" }</c> map.
/// <para>
/// This replaces the resx/<c>ResourceManager</c> pipeline, which returns the raw key under Blazor
/// WebAssembly (satellite assemblies are not loaded in the browser and the embedded neutral
/// <c>.resources</c> is not resolved). Reading embedded JSON works identically under Server and
/// WebAssembly. Lookup walks <see cref="CultureInfo.CurrentUICulture"/> → parent cultures → neutral.
/// </para>
/// </summary>
/// <typeparam name="TResourceSource">Marker type whose assembly carries the JSON resources.</typeparam>
internal sealed class JsonStringLocalizer<TResourceSource> : IStringLocalizer<TResourceSource>
{
    private static readonly Assembly ResourceAssembly = typeof(TResourceSource).Assembly;
    private static readonly string BaseName = typeof(TResourceSource).FullName!;
    private static readonly IReadOnlyDictionary<string, string> Empty =
        new Dictionary<string, string>(StringComparer.Ordinal);

    // culture name ("" = neutral, "cs", "fr", …) → (key → value); each file parsed at most once.
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> Tables =
        new(StringComparer.OrdinalIgnoreCase);

    // resolved most-specific → neutral lookup chains, keyed by CurrentUICulture.Name.
    private static readonly ConcurrentDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> Chains =
        new(StringComparer.OrdinalIgnoreCase);

    public LocalizedString this[string name]
    {
        get
        {
            var value = Find(name);
            return new LocalizedString(name, value ?? name, resourceNotFound: value is null, searchedLocation: BaseName);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var format = Find(name);
            var value = format is null ? name : string.Format(CultureInfo.CurrentCulture, format, arguments);
            return new LocalizedString(name, value, resourceNotFound: format is null, searchedLocation: BaseName);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var chain = ChainFor(CultureInfo.CurrentUICulture);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var table in includeParentCultures ? chain : chain.Take(1))
        {
            foreach (var pair in table)
            {
                if (seen.Add(pair.Key))
                {
                    yield return new LocalizedString(pair.Key, pair.Value, resourceNotFound: false, searchedLocation: BaseName);
                }
            }
        }
    }

    private static string? Find(string name)
    {
        foreach (var table in ChainFor(CultureInfo.CurrentUICulture))
        {
            if (table.TryGetValue(name, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> ChainFor(CultureInfo culture)
        => Chains.GetOrAdd(culture.Name, _ => BuildChain(culture));

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> BuildChain(CultureInfo culture)
    {
        var chain = new List<IReadOnlyDictionary<string, string>>();

        for (var ci = culture; ci is not null && !string.IsNullOrEmpty(ci.Name); ci = ci.Parent)
        {
            AddTable(chain, ci.Name);
            AddTable(chain, ci.TwoLetterISOLanguageName);
        }

        AddTable(chain, string.Empty); // neutral
        return chain;
    }

    private static void AddTable(List<IReadOnlyDictionary<string, string>> chain, string culture)
    {
        var table = Tables.GetOrAdd(culture, Load);
        if (table.Count > 0 && !chain.Contains(table))
        {
            chain.Add(table);
        }
    }

    private static IReadOnlyDictionary<string, string> Load(string culture)
    {
        var resourceName = string.IsNullOrEmpty(culture) ? $"{BaseName}.json" : $"{BaseName}.{culture}.json";

        using var stream = ResourceAssembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return Empty;
        }

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        // JsonDocument (not JsonSerializer.Deserialize) so the parse is reflection-free and survives
        // trimming under published Blazor WebAssembly.
        using var document = JsonDocument.Parse(buffer.ToArray());
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            map[property.Name] = property.Value.GetString() ?? string.Empty;
        }

        return map;
    }
}
