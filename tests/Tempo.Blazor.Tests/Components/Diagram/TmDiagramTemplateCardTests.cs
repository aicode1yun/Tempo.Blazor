using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Templates;
using Tempo.Blazor.Tests.Diagram;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class TmDiagramTemplateCardTests : DiagramTestBase
{
    [Fact]
    public void Renders_Template_Name_And_Tags()
    {
        var template = new DiagramTemplate
        {
            Id = "t1",
            Name = "Flowchart",
            Category = "General",
            Tags = ["process", "basic"]
        };

        var cut = Render<TmDiagramTemplateCard>(parameters => parameters
            .Add(p => p.Template, template)
            .Add(p => p.Selected, false));

        cut.Find(".tm-diagram-template-card__name").TextContent.Should().Be("Flowchart");
        var tags = cut.FindAll(".tm-diagram-template-card__tag");
        tags.Should().HaveCount(2);
        tags[0].TextContent.Should().Be("process");
        tags[1].TextContent.Should().Be("basic");
    }

    [Fact]
    public void Renders_Placeholder_When_No_Thumbnail()
    {
        var template = new DiagramTemplate { Id = "t1", Name = "Blank", Category = "General" };

        var cut = Render<TmDiagramTemplateCard>(parameters => parameters
            .Add(p => p.Template, template)
            .Add(p => p.Selected, false));

        cut.Find(".tm-diagram-template-card__placeholder").TextContent.Should().Be("B");
    }

    [Fact]
    public void Renders_Thumbnail_Image_When_Url_Provided()
    {
        var template = new DiagramTemplate
        {
            Id = "t1",
            Name = "Flowchart",
            Category = "General",
            ThumbnailUrl = "thumb.png"
        };

        var cut = Render<TmDiagramTemplateCard>(parameters => parameters
            .Add(p => p.Template, template)
            .Add(p => p.BaseUri, "https://example.com/")
            .Add(p => p.Selected, false));

        var img = cut.Find("img");
        img.GetAttribute("src").Should().Be("https://example.com/thumb.png");
    }

    [Fact]
    public void Selected_State_Applies_Css_Class()
    {
        var template = new DiagramTemplate { Id = "t1", Name = "Blank", Category = "General" };

        var cut = Render<TmDiagramTemplateCard>(parameters => parameters
            .Add(p => p.Template, template)
            .Add(p => p.Selected, true));

        cut.Find(".tm-diagram-template-card--selected").Should().NotBeNull();
    }

    [Fact]
    public void Click_Invokes_OnClick()
    {
        var clicked = false;
        var template = new DiagramTemplate { Id = "t1", Name = "Blank", Category = "General" };

        var cut = Render<TmDiagramTemplateCard>(parameters => parameters
            .Add(p => p.Template, template)
            .Add(p => p.Selected, false)
            .Add(p => p.OnClick, () => clicked = true));

        cut.Find(".tm-diagram-template-card").Click();

        clicked.Should().BeTrue();
    }
}
