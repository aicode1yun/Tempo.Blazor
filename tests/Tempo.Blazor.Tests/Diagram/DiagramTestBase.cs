using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Diagram;

/// <summary>
/// Base class for all diagram editor bUnit component tests.
/// Sets up the <see cref="DiagramStencilRegistry"/> with built-in stencils
/// and configures loose JS interop so that canvas JS calls do not fail.
/// </summary>
public abstract class DiagramTestBase : LocalizationTestBase
{
    protected DiagramTestBase()
    {
        var registry = new DiagramStencilRegistry();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
        Services.AddSingleton(registry);
    }
}
