using System.Text.Json;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Tests.Wireframe;

/// <summary>
/// Tests for WireframeDocument, WireframeElement, WireframeConnector,
/// WireframeDocumentExtensions, and WireframeJsonOptions.
/// </summary>
public class WireframeDocumentTests
{
    // ── WireframeDocument defaults ─────────────────────────────────────────────

    [Fact]
    public void NewDocument_HasExpectedDefaults()
    {
        var doc = new WireframeDocument();

        doc.Version.Should().Be("2.1");
        doc.Title.Should().Be("Untitled wireframe");
        doc.Width.Should().Be(1280);
        doc.Height.Should().Be(800);
        doc.Elements.Should().BeEmpty();
        doc.Connectors.Should().BeEmpty();
        doc.Pages.Should().HaveCount(1);
        doc.ActivePageId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void NewDocument_CreatedAtIsUtcNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var doc = new WireframeDocument();
        doc.CreatedAt.Should().BeAfter(before);
        doc.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    // ── WireframeElement defaults ──────────────────────────────────────────────

    [Fact]
    public void NewElement_HasUniqueId()
    {
        var el1 = new WireframeElement();
        var el2 = new WireframeElement();
        el1.Id.Should().NotBe(el2.Id);
    }

    [Fact]
    public void NewElement_IdIs8Chars()
    {
        var el = new WireframeElement();
        el.Id.Should().HaveLength(8);
    }

    [Fact]
    public void NewElement_DefaultDimensions()
    {
        var el = new WireframeElement { Type = "TmButton" };
        el.W.Should().Be(120);
        el.H.Should().Be(36);
        el.X.Should().Be(0);
        el.Y.Should().Be(0);
        el.ZIndex.Should().Be(0);
    }

    // ── WireframeConnector defaults ────────────────────────────────────────────

    [Fact]
    public void NewConnector_HasUniqueId()
    {
        var c1 = new WireframeConnector();
        var c2 = new WireframeConnector();
        c1.Id.Should().NotBe(c2.Id);
    }

    [Fact]
    public void NewConnector_LabelIsNull()
    {
        var c = new WireframeConnector();
        c.Label.Should().BeNull();
    }

    // ── WireframeDocumentExtensions.NewElement ─────────────────────────────────

    [Fact]
    public void NewElement_SetsTypeAndPosition()
    {
        var el = WireframeDocumentExtensions.NewElement("TmCard", 50, 100);

        el.Type.Should().Be("TmCard");
        el.X.Should().Be(50);
        el.Y.Should().Be(100);
        el.W.Should().Be(120); // default
        el.H.Should().Be(36);  // default
    }

    [Fact]
    public void NewElement_UsesCustomDimensions()
    {
        var el = WireframeDocumentExtensions.NewElement("TmDataTable", 0, 0, 600, 300);
        el.W.Should().Be(600);
        el.H.Should().Be(300);
    }

    // ── Clone ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Clone_ReturnsDifferentInstance()
    {
        var doc = new WireframeDocument { Title = "Original" };
        var clone = doc.Clone();
        clone.Should().NotBeSameAs(doc);
    }

    [Fact]
    public void Clone_PreservesAllScalarFields()
    {
        var doc = new WireframeDocument { Title = "Test", Width = 1440, Height = 900, Version = "2.0" };
        var clone = doc.Clone();
        clone.Title.Should().Be("Test");
        clone.Width.Should().Be(1440);
        clone.Height.Should().Be(900);
        clone.Version.Should().Be("2.0");
    }

    [Fact]
    public void Clone_DeepCopiesElements()
    {
        var doc = new WireframeDocument();
        var el = WireframeDocumentExtensions.NewElement("TmButton", 10, 20);
        doc.Elements.Add(el);

        var clone = doc.Clone();
        clone.Elements[0].X = 999;

        doc.Elements[0].X.Should().Be(10); // original unaffected
    }

    [Fact]
    public void Clone_DeepCopiesConnectors()
    {
        var doc = new WireframeDocument();
        doc.Connectors.Add(new WireframeConnector { FromId = "a", ToId = "b", Label = "go" });

        var clone = doc.Clone();
        clone.Connectors[0].Label = "changed";

        doc.Connectors[0].Label.Should().Be("go");
    }

    [Fact]
    public void Clone_DeepCopiesElementProps()
    {
        var doc = new WireframeDocument();
        var el = WireframeDocumentExtensions.NewElement("TmCard", 0, 0);
        el.SetProp("title", "Original");
        doc.Elements.Add(el);

        var clone = doc.Clone();
        clone.Elements[0].SetProp("title", "Changed");

        doc.Elements[0].Props.GetString("title").Should().Be("Original");
    }

    // ── SetProp / GetString / GetBool / GetInt / GetDouble / GetStringList ─────

    [Fact]
    public void SetProp_StringValue_CanBeRetrieved()
    {
        var el = new WireframeElement();
        el.SetProp("label", "Submit");
        el.Props.GetString("label").Should().Be("Submit");
    }

    [Fact]
    public void SetProp_IntValue_CanBeRetrieved()
    {
        var el = new WireframeElement();
        el.SetProp("rowCount", 42);
        el.Props.GetInt("rowCount").Should().Be(42);
    }

    [Fact]
    public void SetProp_BoolTrue_CanBeRetrieved()
    {
        var el = new WireframeElement();
        el.SetProp("disabled", true);
        el.Props.GetBool("disabled").Should().BeTrue();
    }

    [Fact]
    public void SetProp_BoolFalse_CanBeRetrieved()
    {
        var el = new WireframeElement();
        el.SetProp("visible", false);
        el.Props.GetBool("visible").Should().BeFalse();
    }

    [Fact]
    public void SetProp_DoubleValue_CanBeRetrieved()
    {
        var el = new WireframeElement();
        el.SetProp("opacity", 0.75);
        el.Props.GetDouble("opacity").Should().BeApproximately(0.75, 0.0001);
    }

    [Fact]
    public void SetProp_StringArray_CanBeRetrievedAsList()
    {
        var el = new WireframeElement();
        el.SetProp("columns", new[] { "Name", "Email", "Role" });
        el.Props.GetStringList("columns").Should().BeEquivalentTo("Name", "Email", "Role");
    }

    [Fact]
    public void SetProp_OverwritesExistingKey()
    {
        var el = new WireframeElement();
        el.SetProp("label", "Old");
        el.SetProp("label", "New");
        el.Props.GetString("label").Should().Be("New");
    }

    [Fact]
    public void SetProp_Null_RemovesKey()
    {
        var el = new WireframeElement();
        el.SetProp("label", "X");
        el.SetProp("label", null);
        el.Props.Should().NotContainKey("label");
    }

    [Fact]
    public void GetString_MissingKey_ReturnsCustomFallback()
    {
        var props = new Dictionary<string, JsonElement>();
        props.GetString("missing", "fallback").Should().Be("fallback");
    }

    [Fact]
    public void GetInt_MissingKey_ReturnsZero()
    {
        var props = new Dictionary<string, JsonElement>();
        props.GetInt("missing").Should().Be(0);
    }

    [Fact]
    public void GetBool_MissingKey_ReturnsFalse()
    {
        var props = new Dictionary<string, JsonElement>();
        props.GetBool("missing").Should().BeFalse();
    }

    [Fact]
    public void GetDouble_MissingKey_ReturnsZero()
    {
        var props = new Dictionary<string, JsonElement>();
        props.GetDouble("missing").Should().Be(0.0);
    }

    [Fact]
    public void GetStringList_MissingKey_ReturnsEmptyArray()
    {
        var props = new Dictionary<string, JsonElement>();
        props.GetStringList("missing").Should().BeEmpty();
    }

    [Fact]
    public void GetStringList_NonArrayValue_ReturnsEmptyArray()
    {
        var el = new WireframeElement();
        el.SetProp("notArray", "just a string");
        el.Props.GetStringList("notArray").Should().BeEmpty();
    }

    // ── WireframeJsonOptions ───────────────────────────────────────────────────

    [Fact]
    public void WireframeJsonOptions_IsNotNull()
    {
        WireframeJsonOptions.Default.Should().NotBeNull();
    }

    [Fact]
    public void WireframeJsonOptions_IsCamelCase()
    {
        WireframeJsonOptions.Default.PropertyNamingPolicy
            .Should().Be(System.Text.Json.JsonNamingPolicy.CamelCase);
    }

    [Fact]
    public void WireframeJsonOptions_WriteIndented()
    {
        WireframeJsonOptions.Default.WriteIndented.Should().BeTrue();
    }

    [Fact]
    public void WireframeJsonOptions_IgnoresNullValues()
    {
        WireframeJsonOptions.Default.DefaultIgnoreCondition
            .Should().Be(System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull);
    }

    // ── ZIndex ordering ────────────────────────────────────────────────────────

    [Fact]
    public void Elements_CanBeOrderedByZIndex()
    {
        var doc = new WireframeDocument();
        var el1 = new WireframeElement { Type = "A", ZIndex = 2 };
        var el2 = new WireframeElement { Type = "B", ZIndex = 0 };
        var el3 = new WireframeElement { Type = "C", ZIndex = 5 };
        doc.Elements.AddRange([el1, el2, el3]);

        var ordered = doc.Elements.OrderBy(e => e.ZIndex).Select(e => e.Type).ToList();
        ordered.Should().Equal("B", "A", "C");
    }

    // ── GroupId / LockedBy ─────────────────────────────────────────────────────

    [Fact]
    public void Element_GroupId_DefaultIsNull()
    {
        new WireframeElement().GroupId.Should().BeNull();
    }

    [Fact]
    public void Element_LockedBy_DefaultIsNull()
    {
        new WireframeElement().LockedBy.Should().BeNull();
    }

    // ── WireframeDeserializationException ──────────────────────────────────────

    [Fact]
    public void WireframeDeserializationException_MessagePreserved()
    {
        var ex = new Tempo.Blazor.Components.Wireframe.WireframeDeserializationException("oops");
        ex.Message.Should().Be("oops");
    }

    [Fact]
    public void WireframeDeserializationException_WithInner_PreservesInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new Tempo.Blazor.Components.Wireframe.WireframeDeserializationException("outer", inner);
        ex.InnerException.Should().BeSameAs(inner);
    }
}
