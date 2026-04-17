using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Components.Diagram.Templates;
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
        var stencilRegistry = new DiagramStencilRegistry();
        stencilRegistry.RegisterProvider(new BuiltInDiagramStencilProvider());
        Services.AddSingleton(stencilRegistry);

        var templateRegistry = new DiagramTemplateRegistry();
        Services.AddSingleton(templateRegistry);
    }
}
