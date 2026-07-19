using System.Reflection;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.Components.Icons;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

/// <summary>
/// Fáze 23 (code review N9.2/N9.3): ComputeRenderSignature toolbaru je ručně udržovaný seznam
/// ~110 vstupů — kontrakt „když přidáš parametr, rozšiř signaturu" neměl žádnou pojistku: nový
/// [Parameter] by propadl, ShouldRender() vrátil false a toolbar tiše zamrzl. Tento reflexní test
/// mutuje KAŽDÝ hodnotový [Parameter] a tvrdí, že komponenta re-renderuje. Druhá část: TmIcon
/// gating s AdditionalAttributes (CaptureUnmatchedValues staví nový dictionary každý parent
/// render — identity hash gating tiše vypínal).
/// </summary>
public class DocumentEditorRenderSignatureSafetyNetTests : LocalizationTestBase
{
    [Fact]
    public void EveryValueParameter_TriggersRerenderWhenChanged()
    {
        var cut = Render<TmDocumentEditorToolbar>();
        var mutable = GetMutableValueParameters();
        mutable.Should().HaveCountGreaterThan(50, "toolbar má ~75 hodnotových parametrů — příliš nízký počet značí rozbitou detekci");

        var missing = new List<string>();
        foreach (var parameter in mutable)
        {
            var currentValue = parameter.GetValue(cut.Instance);
            var changedValue = BuildChangedValue(parameter.PropertyType, currentValue);
            if (changedValue is null && Nullable.GetUnderlyingType(parameter.PropertyType) is null)
            {
                continue; // nelze bezpečně vyrobit odlišnou hodnotu — typ by musel být přidán níže
            }

            var before = cut.RenderCount;
            cut.Render(Microsoft.AspNetCore.Components.ParameterView.FromDictionary(
                new Dictionary<string, object?> { [parameter.Name] = changedValue }));
            if (cut.RenderCount == before)
            {
                missing.Add($"{parameter.Name} ({parameter.PropertyType.Name})");
            }
        }

        missing.Should().BeEmpty(
            "každý hodnotový [Parameter] musí být zahrnut v ComputeRenderSignature — jinak jeho změna toolbar tiše nezpropaguje (ShouldRender=false)");
    }

    [Fact]
    public void TmIcon_EqualContentAttributesDictionary_SkipsRerender()
    {
        // CaptureUnmatchedValues: rodič staví NOVÝ dictionary při každém renderu — gating musí
        // hashovat obsah, ne referenci, jinak pro ikony s atributy nikdy nefunguje.
        var cut = Render<TmIcon>(p => p
            .Add(c => c.Name, IconNames.Check)
            .AddUnmatched("data-role", "toolbar-icon")
            .AddUnmatched("title", "Check"));
        var before = cut.RenderCount;

        cut.Render(p => p
            .Add(c => c.Name, IconNames.Check)
            .AddUnmatched("data-role", "toolbar-icon")
            .AddUnmatched("title", "Check"));

        cut.RenderCount.Should().Be(before,
            "nový dictionary se stejným obsahem nesmí shodit render gating (N9.3)");
    }

    [Fact]
    public void TmIcon_ChangedAttributeContent_TriggersRerender()
    {
        var cut = Render<TmIcon>(p => p
            .Add(c => c.Name, IconNames.Check)
            .AddUnmatched("title", "Check"));
        var before = cut.RenderCount;

        cut.Render(p => p
            .Add(c => c.Name, IconNames.Check)
            .AddUnmatched("title", "Changed"));

        cut.RenderCount.Should().BeGreaterThan(before, "změna obsahu atributu musí re-renderovat");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static List<PropertyInfo> GetMutableValueParameters()
        => typeof(TmDocumentEditorToolbar)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetCustomAttribute<ParameterAttribute>() is { CaptureUnmatchedValues: false })
            .Where(property => property.CanWrite)
            .Where(property => IsSupportedValueType(property.PropertyType))
            .ToList();

    private static bool IsSupportedValueType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying == typeof(bool)
            || underlying == typeof(string)
            || underlying == typeof(int)
            || underlying == typeof(double)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying.IsEnum;
    }

    private static object? BuildChangedValue(Type type, object? current)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(bool))
        {
            return !(current as bool? ?? false);
        }

        if (underlying == typeof(string))
        {
            return (current as string ?? string.Empty) + "-changed";
        }

        if (underlying == typeof(int))
        {
            return (current as int? ?? 0) + 7;
        }

        if (underlying == typeof(double))
        {
            return (current as double? ?? 0d) + 7.5d;
        }

        if (underlying == typeof(DateTime))
        {
            return (current as DateTime? ?? new DateTime(2026, 1, 1)).AddMinutes(1);
        }

        if (underlying == typeof(DateTimeOffset))
        {
            return (current as DateTimeOffset? ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).AddMinutes(1);
        }

        if (underlying.IsEnum)
        {
            var values = Enum.GetValues(underlying).Cast<object>().ToList();
            return values.FirstOrDefault(value => !Equals(value, current)) ?? current;
        }

        return null;
    }
}
