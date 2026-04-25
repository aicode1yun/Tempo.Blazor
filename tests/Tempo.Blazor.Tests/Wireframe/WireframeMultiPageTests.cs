using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Tests.Wireframe;

/// <summary>
/// Tests for the multi-page document model introduced in Phase 6.
/// </summary>
public class WireframeMultiPageTests
{
    // ── Document defaults ─────────────────────────────────────────────────────

    [Fact]
    public void NewDocument_HasNoPages_UntilAccessed()
    {
        var doc = new WireframeDocument();

        // Raw Pages list is empty until a convenience accessor triggers EnsureActivePage
        doc.Pages.Should().BeEmpty();
    }

    [Fact]
    public void NewDocument_AccessingElements_CreatesDefaultPage()
    {
        var doc = new WireframeDocument();

        _ = doc.Elements; // triggers EnsureActivePage

        doc.Pages.Should().HaveCount(1);
        doc.ActivePageId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void NewDocument_DefaultPage_HasExpectedDimensions()
    {
        var doc = new WireframeDocument();

        doc.Width.Should().Be(1280);
        doc.Height.Should().Be(800);
        doc.Elements.Should().BeEmpty();
        doc.Connectors.Should().BeEmpty();
    }

    // ── Page management ───────────────────────────────────────────────────────

    [Fact]
    public void AddPage_IncreasesPageCount()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        var page = new WireframePage { Name = "Page 2" };

        doc.Pages.Add(page);

        doc.Pages.Should().HaveCount(2);
    }

    [Fact]
    public void DeletePage_RemovesPage()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        var page = new WireframePage { Name = "Page 2" };
        doc.Pages.Add(page);

        doc.Pages.Remove(page);

        doc.Pages.Should().HaveCount(1);
    }

    [Fact]
    public void DeletePage_UpdatesActivePageId_WhenDeletedWasActive()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        var page1 = doc.ActivePage!;
        var page2 = new WireframePage { Name = "Page 2" };
        doc.Pages.Add(page2);
        doc.ActivePageId = page2.Id;

        doc.Pages.Remove(page2);
        doc.EnsureActivePage();

        doc.ActivePageId.Should().Be(page1.Id);
    }

    [Fact]
    public void SwitchPage_ChangesActivePage()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        var page2 = new WireframePage { Name = "Page 2" };
        doc.Pages.Add(page2);

        doc.ActivePageId = page2.Id;

        doc.ActivePage!.Name.Should().Be("Page 2");
    }

    [Fact]
    public void RenamePage_UpdatesName()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        var page = doc.ActivePage!;

        page.Name = "Home";

        page.Name.Should().Be("Home");
    }

    [Fact]
    public void DuplicatePage_DeepCopiesElements()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        var el = WireframeDocumentExtensions.NewElement("TmButton", 10, 20);
        doc.Elements.Add(el);

        var json = System.Text.Json.JsonSerializer.Serialize(doc.ActivePage!);
        var copy = System.Text.Json.JsonSerializer.Deserialize<WireframePage>(json)!;
        copy.Id = "p" + Guid.NewGuid().ToString("N")[..7];
        doc.Pages.Add(copy);

        doc.Pages.Should().HaveCount(2);
        doc.Pages[1].Elements.Should().HaveCount(1);
        doc.Pages[1].Elements[0].Should().NotBeSameAs(el); // deep copy, different instance
    }

    [Fact]
    public void ReorderPages_ChangesOrder()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        var page2 = new WireframePage { Name = "Page 2" };
        var page3 = new WireframePage { Name = "Page 3" };
        doc.Pages.Add(page2);
        doc.Pages.Add(page3);

        var moved = doc.Pages[2];
        doc.Pages.RemoveAt(2);
        doc.Pages.Insert(0, moved);

        doc.Pages[0].Name.Should().Be("Page 3");
        doc.Pages[1].Name.Should().Be("Page 1");
        doc.Pages[2].Name.Should().Be("Page 2");
    }

    // ── Page isolation ────────────────────────────────────────────────────────

    [Fact]
    public void Elements_AreIsolated_PerPage()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        var page1 = doc.ActivePage!;
        var page2 = new WireframePage { Name = "Page 2" };
        doc.Pages.Add(page2);

        doc.Elements.Add(WireframeDocumentExtensions.NewElement("TmButton", 0, 0));
        doc.ActivePageId = page2.Id;
        doc.Elements.Add(WireframeDocumentExtensions.NewElement("TmCard", 100, 100));

        page1.Elements.Should().HaveCount(1);
        page2.Elements.Should().HaveCount(1);
    }

    [Fact]
    public void Connectors_AreIsolated_PerPage()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        var page1 = doc.ActivePage!;
        var page2 = new WireframePage { Name = "Page 2" };
        doc.Pages.Add(page2);

        doc.Connectors.Add(new WireframeConnector { FromId = "a", ToId = "b" });
        doc.ActivePageId = page2.Id;
        doc.Connectors.Add(new WireframeConnector { FromId = "c", ToId = "d" });

        page1.Connectors.Should().HaveCount(1);
        page2.Connectors.Should().HaveCount(1);
    }

    [Fact]
    public void WidthHeight_AreIsolated_PerPage()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        var page2 = new WireframePage { Name = "Page 2", Width = 1920, Height = 1080 };
        doc.Pages.Add(page2);

        doc.Width.Should().Be(1280); // page1 is still active
        doc.ActivePageId = page2.Id;
        doc.Width.Should().Be(1920);
        doc.Height.Should().Be(1080);
    }

    // ── Serialization ─────────────────────────────────────────────────────────

    [Fact]
    public void Serialize_IncludesPagesArray()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        var page2 = new WireframePage { Name = "Page 2" };
        doc.Pages.Add(page2);

        var json = WireframeSerializer.Serialize(doc);

        json.Should().Contain("\"pages\"");
        json.Should().Contain("\"activePageId\"");
    }

    [Fact]
    public void Serialize_DoesNotIncludeElementsDirectlyOnRoot()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        doc.Elements.Add(WireframeDocumentExtensions.NewElement("TmButton", 0, 0));

        var json = WireframeSerializer.Serialize(doc);

        // Elements should be inside pages[0], not on root
        using var jdoc = System.Text.Json.JsonDocument.Parse(json);
        jdoc.RootElement.TryGetProperty("elements", out _).Should().BeFalse("elements must not appear on root in v2.0");
        jdoc.RootElement.GetProperty("pages")[0].TryGetProperty("elements", out _).Should().BeTrue();
    }

    [Fact]
    public void Roundtrip_PreservesMultiplePages()
    {
        var original = new WireframeDocument { Title = "Multi" };
        original.EnsureActivePage();
        original.Elements.Add(WireframeDocumentExtensions.NewElement("TmButton", 0, 0));
        var page2 = new WireframePage { Name = "Page 2", Width = 1920, Height = 1080 };
        page2.Elements.Add(WireframeDocumentExtensions.NewElement("TmCard", 50, 50));
        original.Pages.Add(page2);

        var json = WireframeSerializer.Serialize(original);
        var restored = WireframeSerializer.Deserialize(json);

        restored.Pages.Should().HaveCount(2);
        restored.Pages[0].Elements.Should().HaveCount(1);
        restored.Pages[1].Elements.Should().HaveCount(1);
        restored.Pages[1].Width.Should().Be(1920);
        restored.Pages[1].Name.Should().Be("Page 2");
    }

    // ── Convenience accessors ─────────────────────────────────────────────────

    [Fact]
    public void Elements_Getter_AutoCreatesDefaultPage()
    {
        var doc = new WireframeDocument { Pages = [] };
        doc.Elements.Add(WireframeDocumentExtensions.NewElement("TmButton", 0, 0));

        doc.Pages.Should().HaveCount(1);
        doc.Elements.Should().HaveCount(1);
    }

    [Fact]
    public void WidthHeight_Setter_AutoCreatesDefaultPage()
    {
        var doc = new WireframeDocument { Pages = [] };
        doc.Width = 1440;
        doc.Height = 900;

        doc.Pages.Should().HaveCount(1);
        doc.Width.Should().Be(1440);
        doc.Height.Should().Be(900);
    }

    [Fact]
    public void EnsureActivePage_CreatesPage_WhenEmpty()
    {
        var doc = new WireframeDocument { Pages = [] };

        doc.EnsureActivePage();

        doc.Pages.Should().HaveCount(1);
        doc.ActivePageId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void EnsureActivePage_FixesInvalidActivePageId()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        doc.Pages.Clear();
        var page = new WireframePage { Name = "Only" };
        doc.Pages.Add(page);
        doc.ActivePageId = "non-existent";

        doc.EnsureActivePage();

        doc.ActivePageId.Should().Be(page.Id);
    }
}
