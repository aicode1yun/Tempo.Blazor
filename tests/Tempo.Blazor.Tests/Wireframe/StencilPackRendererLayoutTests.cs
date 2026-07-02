using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Tempo.Blazor.Components.Wireframe.Stencil;

namespace Tempo.Blazor.Tests.Wireframe;

public class StencilPackRendererLayoutTests
{
    [Fact]
    public async Task Group_AppliesTransformAndOpacity_AndSkipsWhenFalseChild()
    {
        var group = new RenderNode
        {
            Kind = RenderNodeKind.Group,
            Attributes = Attrs(("x", 10), ("y", 20), ("opacity", 0.5)),
            Children =
            [
                Node(RenderNodeKind.Rect, ("w", 20), ("h", 10), ("fill", "#22c55e")),
                new RenderNode
                {
                    Kind = RenderNodeKind.Rect,
                    When = "{false}",
                    Attributes = Attrs(("w", 20), ("h", 10), ("fill", "#ef4444"))
                }
            ]
        };

        var svg = await RenderAsync(Component(group), Element());

        svg.Should().Contain("<g transform='translate(10,20)' opacity='0.5'>");
        svg.Should().Contain("fill='#22c55e'");
        svg.Should().NotContain("#ef4444");
        svg.Should().NotContainEquivalentOf("<script");
        svg.Should().NotContainEquivalentOf("<foreignObject");
    }

    [Fact]
    public async Task Stack_RendersChildrenAtGapOffsets()
    {
        var stack = new RenderNode
        {
            Kind = RenderNodeKind.Stack,
            Attributes = Attrs(("gap", 8), ("padding", 0)),
            Children =
            [
                Node(RenderNodeKind.Rect, ("w", 20), ("h", 20)),
                Node(RenderNodeKind.Rect, ("w", 20), ("h", 20)),
                Node(RenderNodeKind.Rect, ("w", 20), ("h", 20))
            ]
        };

        var svg = await RenderAsync(Component(stack), Element());

        svg.Should().Contain("translate(0,0)");
        svg.Should().Contain("translate(0,28)");
        svg.Should().Contain("translate(0,56)");
    }

    [Fact]
    public async Task Repeat_CapEnforced_LogsDropped()
    {
        var logger = new CaptureLogger();
        var repeat = new RenderNode
        {
            Kind = RenderNodeKind.Repeat,
            Attributes = Attrs(("count", 100), ("max", 10), ("as", "i")),
            Node = Node(RenderNodeKind.Text, ("content", "{i}"), ("x", 0), ("y", 10))
        };

        var svg = await RenderAsync(Component(repeat), Element(), logger);

        Regex.Matches(svg, "<text").Count.Should().Be(10);
        logger.Messages.Should().Contain(x => x.Contains("90", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Resize_9slice_DoesNotDistortCorners()
    {
        var component = new StencilComponent
        {
            Type = "test:Layout",
            DisplayName = "Layout",
            Category = "Tests",
            DefaultSize = new StencilSize(120, 36),
            Render = Node(RenderNodeKind.Rect, ("fill", "#f3f4f6"), ("stroke", "#d1d5db")),
            Resize = StencilResize.NineSlice,
            Slice = new StencilSlice { Left = 16, Top = 16, Right = 16, Bottom = 16 }
        };

        var svg = await RenderAsync(component, Element(w: 400, h: 80));

        Regex.Matches(svg, "width='16' height='16'").Count.Should().Be(4);
        svg.Should().Contain("width='368' height='16'");
    }

    [Fact]
    public async Task RightAnchoredNode_PinsToElementWidth()
    {
        var group = new RenderNode
        {
            Kind = RenderNodeKind.Group,
            Children =
            [
                Node(RenderNodeKind.Rect, ("w", 40), ("h", 20), ("anchor", "right"), ("margin.right", 10))
            ]
        };

        var svg = await RenderAsync(Component(group), Element(w: 200, h: 80));

        svg.Should().Contain("translate(150,0)");
    }

    [Fact]
    public async Task Render_IsDeterministic()
    {
        var grid = new RenderNode
        {
            Kind = RenderNodeKind.Grid,
            Attributes = Attrs(("columns", 2), ("gap", 4), ("padding", 2)),
            Children =
            [
                Node(RenderNodeKind.Rect, ("h", 12)),
                Node(RenderNodeKind.Text, ("content", "A"), ("h", 12), ("y", 6))
            ]
        };
        var component = Component(grid);
        var element = Element(w: 100, h: 60);

        var first = await RenderAsync(component, element);
        var second = await RenderAsync(component, element);

        second.Should().Be(first);
    }

    private static RenderNode Node(RenderNodeKind kind, params (string Key, object? Value)[] attributes)
        => new()
        {
            Kind = kind,
            Attributes = Attrs(attributes)
        };

    private static Dictionary<string, object?> Attrs(params (string Key, object? Value)[] attributes)
        => attributes.ToDictionary(x => x.Key, x => x.Value);

    private static StencilComponent Component(RenderNode render)
        => new()
        {
            Type = "test:Layout",
            DisplayName = "Layout",
            Category = "Tests",
            DefaultSize = new StencilSize(120, 36),
            Render = render
        };

    private static WireframeElement Element(double w = 120, double h = 36, params (string Key, object? Value)[] props)
    {
        var element = new WireframeElement { Type = "test:Layout", W = w, H = h };
        foreach (var (key, value) in props)
            element.Props[key] = JsonSerializer.SerializeToElement(value);
        return element;
    }

    private static async Task<string> RenderAsync(
        StencilComponent component,
        WireframeElement element,
        ILogger? logger = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var htmlRenderer = new HtmlRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);

        return await htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            RenderFragment fragment = builder =>
            {
                builder.OpenElement(0, "svg");
                builder.AddAttribute(1, "xmlns", "http://www.w3.org/2000/svg");
                builder.AddAttribute(2, "viewBox", $"0 0 {element.W} {element.H}");
                StencilPackRenderer.Render(component, element, StencilTokenScope.Empty, builder, logger);
                builder.CloseElement();
            };

            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?> { ["Content"] = fragment });
            var output = await htmlRenderer.RenderComponentAsync<FragmentHost>(parameters);
            return output.ToHtmlString();
        });
    }

    private sealed class FragmentHost : ComponentBase
    {
        [Parameter] public RenderFragment? Content { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
            => Content?.Invoke(builder);
    }

    private sealed class CaptureLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
