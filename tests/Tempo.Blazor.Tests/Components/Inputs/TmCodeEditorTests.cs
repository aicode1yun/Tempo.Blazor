using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Inputs;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Inputs;

/// <summary>Tests for TmCodeEditor.</summary>
public class TmCodeEditorTests : LocalizationTestBase
{
    [Fact]
    public void TmCodeEditor_Renders_Textarea_And_Overlay()
    {
        var cut = RenderComponent<TmCodeEditor>();
        cut.Find("textarea").ClassList.Should().Contain("tm-code-editor__textarea");
        cut.Find("pre.tm-code-editor__highlight").Should().NotBeNull();
    }

    [Fact]
    public void TmCodeEditor_Root_Has_Default_TestId()
    {
        var cut = RenderComponent<TmCodeEditor>();
        cut.Find("[data-testid='code-editor']").Should().NotBeNull();
    }

    [Fact]
    public void TmCodeEditor_DataTestId_Overrides_Root_TestId()
    {
        var cut = RenderComponent<TmCodeEditor>(p => p.Add(c => c.DataTestId, "job-attributes"));
        cut.Find("[data-testid='job-attributes']").Should().NotBeNull();
    }

    [Fact]
    public void TmCodeEditor_Default_Rows_Is_8()
    {
        var cut = RenderComponent<TmCodeEditor>();
        cut.Find("textarea").GetAttribute("rows").Should().Be("8");
    }

    [Fact]
    public void TmCodeEditor_Label_Renders_Label_Element()
    {
        var cut = RenderComponent<TmCodeEditor>(p => p.Add(c => c.Label, "Attributes"));
        cut.Find("label").TextContent.Trim().Should().Be("Attributes");
    }

    [Fact]
    public void TmCodeEditor_Placeholder_Applied()
    {
        var cut = RenderComponent<TmCodeEditor>(p => p.Add(c => c.Placeholder, "{ }"));
        cut.Find("textarea").GetAttribute("placeholder").Should().Be("{ }");
    }

    [Fact]
    public void TmCodeEditor_Disabled_Sets_Disabled_Attribute()
    {
        var cut = RenderComponent<TmCodeEditor>(p => p.Add(c => c.Disabled, true));
        cut.Find("textarea").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void TmCodeEditor_ReadOnly_Sets_Readonly_Attribute()
    {
        var cut = RenderComponent<TmCodeEditor>(p => p.Add(c => c.ReadOnly, true));
        cut.Find("textarea").HasAttribute("readonly").Should().BeTrue();
    }

    [Fact]
    public void TmCodeEditor_Language_Sets_Highlight_Language_Class()
    {
        var cut = RenderComponent<TmCodeEditor>(p => p.Add(c => c.Language, "json"));
        cut.Find("pre.tm-code-editor__highlight code").ClassList.Should().Contain("language-json");
    }

    [Fact]
    public void TmCodeEditor_ValueChanged_Fires_On_Input()
    {
        string? captured = null;
        var cut = RenderComponent<TmCodeEditor>(p => p
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<string>(this, v => captured = v)));

        cut.Find("textarea").Input("{\"a\":1}");

        captured.Should().Be("{\"a\":1}");
    }

    [Fact]
    public void TmCodeEditor_CopyButton_Rendered_By_Default()
    {
        var cut = RenderComponent<TmCodeEditor>();
        cut.FindAll(".tm-code-editor__copy").Should().NotBeEmpty();
    }

    [Fact]
    public void TmCodeEditor_CopyButton_Hidden_When_Disabled_By_Parameter()
    {
        var cut = RenderComponent<TmCodeEditor>(p => p.Add(c => c.ShowCopyButton, false));
        cut.FindAll(".tm-code-editor__copy").Should().BeEmpty();
    }

    // ── JSON validation ──────────────────────────────────────────

    [Fact]
    public void TmCodeEditor_ValidateJson_Shows_Error_For_Invalid_Json()
    {
        var cut = RenderComponent<TmCodeEditor>(p => p
            .Add(c => c.Language, "json")
            .Add(c => c.ValidateJson, true));

        cut.Find("textarea").Input("{not json");

        cut.Find("[data-testid='error-message']").TextContent.Should().Contain("Invalid JSON");
        cut.Instance.IsValid.Should().BeFalse();
        cut.Find("textarea").GetAttribute("aria-invalid").Should().Be("true");
    }

    [Fact]
    public void TmCodeEditor_ValidateJson_No_Error_For_Valid_Json()
    {
        var cut = RenderComponent<TmCodeEditor>(p => p
            .Add(c => c.Language, "json")
            .Add(c => c.ValidateJson, true));

        cut.Find("textarea").Input("{\"a\": [1, 2, 3]}");

        cut.FindAll("[data-testid='error-message']").Should().BeEmpty();
        cut.Instance.IsValid.Should().BeTrue();
    }

    [Fact]
    public void TmCodeEditor_ValidateJson_Ignores_Empty_Value()
    {
        var cut = RenderComponent<TmCodeEditor>(p => p
            .Add(c => c.Language, "json")
            .Add(c => c.ValidateJson, true));

        cut.FindAll("[data-testid='error-message']").Should().BeEmpty();
        cut.Instance.IsValid.Should().BeTrue();
    }

    [Fact]
    public void TmCodeEditor_ValidateJson_Inactive_For_Other_Languages()
    {
        var cut = RenderComponent<TmCodeEditor>(p => p
            .Add(c => c.Language, "csharp")
            .Add(c => c.ValidateJson, true));

        cut.Find("textarea").Input("{not json");

        cut.FindAll("[data-testid='error-message']").Should().BeEmpty();
    }

    [Fact]
    public void TmCodeEditor_External_Error_Takes_Precedence_Over_Json_Error()
    {
        var cut = RenderComponent<TmCodeEditor>(p => p
            .Add(c => c.Language, "json")
            .Add(c => c.ValidateJson, true)
            .Add(c => c.Error, "Server rejected the value"));

        cut.Find("textarea").Input("{not json");

        cut.Find("[data-testid='error-message']").TextContent.Should().Be("Server rejected the value");
    }

    // ── Format button ────────────────────────────────────────────

    [Fact]
    public void TmCodeEditor_FormatButton_Rendered_Only_For_Json()
    {
        var json = RenderComponent<TmCodeEditor>(p => p
            .Add(c => c.Language, "json")
            .Add(c => c.ShowFormatButton, true));
        json.FindAll("[data-testid='format-button']").Should().NotBeEmpty();

        var csharp = RenderComponent<TmCodeEditor>(p => p
            .Add(c => c.Language, "csharp")
            .Add(c => c.ShowFormatButton, true));
        csharp.FindAll("[data-testid='format-button']").Should().BeEmpty();
    }

    [Fact]
    public async Task TmCodeEditor_FormatJsonAsync_PrettyPrints_Value()
    {
        string? captured = null;
        var cut = RenderComponent<TmCodeEditor>(p => p
            .Add(c => c.Value, "{\"a\":1,\"b\":[true,null]}")
            .Add(c => c.Language, "json")
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<string>(this, v => captured = v)));

        await cut.InvokeAsync(() => cut.Instance.FormatJsonAsync());

        captured.Should().Contain("\n");
        captured.Should().Contain("\"a\": 1");
    }

    [Fact]
    public async Task TmCodeEditor_FormatJsonAsync_NoOp_For_Invalid_Json()
    {
        string? captured = null;
        var cut = RenderComponent<TmCodeEditor>(p => p
            .Add(c => c.Value, "{broken")
            .Add(c => c.Language, "json")
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<string>(this, v => captured = v)));

        await cut.InvokeAsync(() => cut.Instance.FormatJsonAsync());

        captured.Should().BeNull();
    }

    // ── TestIdPrefix ─────────────────────────────────────────────

    [Fact]
    public void TmCodeEditor_TestIdPrefix_Namespaces_Internal_TestIds()
    {
        var cut = RenderComponent<TmCodeEditor>(p => p
            .Add(c => c.TestIdPrefix, "job-attrs")
            .Add(c => c.Language, "json")
            .Add(c => c.ValidateJson, true));

        cut.Find("[data-testid='job-attrs-code-editor']").Should().NotBeNull();

        cut.Find("textarea").Input("{invalid");
        cut.Find("[data-testid='job-attrs-error-message']").Should().NotBeNull();
    }
}
