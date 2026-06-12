using Microsoft.AspNetCore.Components;
using Tempo.Blazor.EmailTemplates;

namespace Tempo.Blazor.EmailTemplates.Tests;

public class SmokeTests : TestContext
{
    [Fact]
    public void UiAssembly_IsResolvable()
    {
        EmailTemplatesUI.Assembly.GetName().Name
            .Should().Be("Tempo.Blazor.EmailTemplates");
    }

    [Fact]
    public void BunitTestContext_RendersMarkup()
    {
        var cut = Render((RenderFragment)(builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddContent(1, "ok");
            builder.CloseElement();
        }));

        cut.Markup.Should().Contain("ok");
    }
}
