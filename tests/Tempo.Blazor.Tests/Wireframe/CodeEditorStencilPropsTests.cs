using FluentAssertions;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Wireframe;

/// <summary>
/// The code editor can wrap long lines (TmCodeEditor.Wrap), and a wireframe that mocks up a form
/// with a prose-like editor has to be able to say so — otherwise the wireframe and the implemented
/// screen disagree about a visible layout property.
/// </summary>
public class CodeEditorStencilPropsTests
{
    private static WireframeComponentRegistry Registry()
    {
        var registry = new WireframeComponentRegistry();
        registry.RegisterProvider(new BuiltInStencilPackProvider());
        return registry;
    }

    [Fact]
    public void Schema_Exposes_Wrap_As_Boolean_Prop_Defaulting_To_False()
    {
        var schema = new BuiltInComponentSchemas().GetSchemas().Single(s => s.Type == "TmCodeEditor");

        var wrap = schema.Props.SingleOrDefault(p => p.Name == "wrap");

        wrap.Should().NotBeNull("the wireframe must be able to mock up a wrapping code editor");
        wrap!.Type.Should().Be(PropType.Bool);
        wrap.Default.Should().Be(false, "wrapping is opt-in, exactly like the component parameter");
    }

    [Fact]
    public void PackDefinition_Carries_The_Wrap_Prop()
    {
        // The pack pulls prop metadata from the built-in schemas — a prop added to only one of them
        // would show up in the property panel but never reach the definition (or the other way round).
        var def = Registry().GetDef("TmCodeEditor");

        def.Should().NotBeNull();
        def!.Props.Select(p => p.Name).Should().Contain("wrap");
    }
}
