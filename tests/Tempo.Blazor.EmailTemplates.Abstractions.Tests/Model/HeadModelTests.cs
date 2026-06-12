using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;
using Tempo.Blazor.EmailTemplates.Abstractions.Serialization;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Tests.Model;

public class HeadModelTests
{
    [Fact]
    public void ExtraAttributes_SurviveRoundTrip()
    {
        var doc = new EmailTemplateDocument();
        var section = new EmailSection();
        var col = new EmailColumn();
        var text = new EmailTextBlock();
        text.ExtraAttributes["data-tracking"] = "abc";
        col.ExtraAttributes["data-col"] = "1";
        col.Blocks.Add(text);
        section.ExtraAttributes["data-sec"] = "2";
        section.Columns.Add(col);
        doc.Sections.Add(section);

        var back = EmailTemplateSerializer.Deserialize(EmailTemplateSerializer.Serialize(doc));

        var bs = back.Sections[0];
        bs.ExtraAttributes["data-sec"].Should().Be("2");
        bs.Columns[0].ExtraAttributes["data-col"].Should().Be("1");
        bs.Columns[0].Blocks[0].ExtraAttributes["data-tracking"].Should().Be("abc");
    }

    [Fact]
    public void MjAttributes_Resolve_FollowsClassOverPerTagOverAll()
    {
        var attrs = new MjAttributes();
        attrs.All["font-family"] = "Arial";
        attrs.PerTag["mj-text"] = new() { ["color"] = "blue", ["font-family"] = "Georgia" };
        attrs.Classes["big"] = new() { ["font-size"] = "20px" };

        // per-tag beats mj-all
        attrs.Resolve("mj-text", Array.Empty<string>(), "font-family").Should().Be("Georgia");
        // mj-all used when no per-tag entry
        attrs.Resolve("mj-button", Array.Empty<string>(), "font-family").Should().Be("Arial");
        // class supplies its own attribute
        attrs.Resolve("mj-text", new[] { "big" }, "font-size").Should().Be("20px");
        // unknown attribute resolves to null
        attrs.Resolve("mj-text", Array.Empty<string>(), "border").Should().BeNull();
    }

    [Fact]
    public void MjAttributes_Resolve_LaterClassWins()
    {
        var attrs = new MjAttributes();
        attrs.Classes["a"] = new() { ["color"] = "red" };
        attrs.Classes["b"] = new() { ["color"] = "green" };

        attrs.Resolve("mj-text", new[] { "a", "b" }, "color").Should().Be("green");
        attrs.Resolve("mj-text", new[] { "b", "a" }, "color").Should().Be("red");
    }

    [Fact]
    public void HtmlAttributes_AreModelledAndRoundTrip()
    {
        var doc = new EmailTemplateDocument();
        var selector = new MjHtmlSelector { Path = ".promo" };
        selector.Attributes["data-id"] = "42";
        doc.Styles.HtmlAttributes.Add(selector);

        var back = EmailTemplateSerializer.Deserialize(EmailTemplateSerializer.Serialize(doc));

        back.Styles.HtmlAttributes.Should().ContainSingle()
            .Which.Attributes["data-id"].Should().Be("42");
    }

    [Fact]
    public void Head_FontsStylesBreakpointAndAttributes_RoundTrip()
    {
        var doc = SampleDocuments.FullyPopulated();
        doc.Styles.Breakpoint = "600px";
        doc.Styles.Attributes.All["font-family"] = "Roboto";
        doc.Styles.Attributes.Classes["cta"] = new() { ["background-color"] = "#f00" };

        var back = EmailTemplateSerializer.Deserialize(EmailTemplateSerializer.Serialize(doc));

        back.Styles.Breakpoint.Should().Be("600px");
        back.Styles.Fonts.Should().ContainSingle().Which.Href.Should().Be("https://fonts/r.css");
        back.Styles.Styles[0].Inline.Should().BeTrue();
        back.Styles.Attributes.All["font-family"].Should().Be("Roboto");
        back.Styles.Attributes.Classes["cta"]["background-color"].Should().Be("#f00");
    }

    [Fact]
    public void RichlyConfiguredBlock_RoundTripsEveryAttribute()
    {
        // E1.32 parity spot-check: a fully customised button preserves all its attributes.
        var button = new EmailButtonBlock
        {
            Text = "Buy", Href = "https://shop", BackgroundColor = "#0a0", Color = "#fff",
            FontFamily = "Georgia", FontSize = "16px", FontWeight = "bold", LineHeight = "150%",
            BorderRadius = "8px", Border = "1px solid #000", InnerPadding = "12px 30px",
            Align = "left", VerticalAlign = "top", Width = "200px", Height = "44px",
            TextDecoration = "underline", TextTransform = "uppercase", Target = "_self",
            Padding = "5px", ContainerBackgroundColor = "#eee", CssClass = "cta",
        };
        button.MjClasses.Add("big");

        var clone = (EmailButtonBlock)button.Clone();

        clone.Should().BeEquivalentTo(button);
    }
}
