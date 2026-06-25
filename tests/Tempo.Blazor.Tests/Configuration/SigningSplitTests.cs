using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tempo.Blazor.Components.Inputs;
using Tempo.Blazor.Components.Signing;
using Tempo.Blazor.Configuration;
using Tempo.Blazor.Localization;
using Tempo.Blazor.Services;

namespace Tempo.Blazor.Tests.Configuration;

public sealed class SigningSplitTests
{
    private static readonly string[] SigningCssFiles =
    [
        "_document-page-viewer.css",
        "_document-comments.css",
        "_signing-field-overlay.css",
        "_condition-builder.css",
        "_formula-builder.css",
        "_recipient-role-editor.css",
        "_signing-field-editor-panel.css",
        "_pdf-template-designer.css",
        "_signing-step-shell.css",
        "_signing-form-runner.css",
        "_signing-completion-panel.css",
        "_submission-status-timeline.css",
        "_share-link-panel.css",
        "_pdf-signature-verification.css",
        "_audit-trail-viewer.css"
    ];

    [Fact]
    public void AddTempoBlazorSigning_registers_core_services_from_signing_package()
    {
        var services = CreateServices();

        services.AddTempoBlazorSigning();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ITmLocalizer>().Should().NotBeNull();
        provider.GetRequiredService<ThemeService>().Should().NotBeNull();
        provider.GetRequiredService<ToastService>().Should().NotBeNull();
        provider.GetRequiredService<DragDropService>().Should().NotBeNull();
        typeof(SigningServiceCollectionExtensions).Assembly.GetName().Name.Should().Be("Tempo.Blazor.Signing");
        typeof(TmPdfTemplateDesigner).Assembly.GetName().Name.Should().Be("Tempo.Blazor.Signing");
        typeof(TmSigningFormRunner).Assembly.GetName().Name.Should().Be("Tempo.Blazor.Signing");
    }

    [Fact]
    public void AddTempoBlazorAll_references_signing_package_and_keeps_signing_registration_callable()
    {
        typeof(AllServiceCollectionExtensions).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .Should()
            .Contain("Tempo.Blazor.Signing");

        var services = CreateServices();

        services.AddTempoBlazorAll();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ITmLocalizer>().Should().NotBeNull();
        typeof(TmAuditTrailViewer).Assembly.GetName().Name.Should().Be("Tempo.Blazor.Signing");
    }

    [Fact]
    public void AddTempoBlazor_core_package_does_not_reference_signing_package()
    {
        typeof(ServiceCollectionExtensions).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .Should()
            .NotContain("Tempo.Blazor.Signing");

        typeof(ServiceCollectionExtensions)
            .GetMethods()
            .Select(method => method.Name)
            .Should()
            .NotContain("AddTempoBlazorSigning");
    }

    [Fact]
    public void Signing_static_assets_are_owned_by_signing_package()
    {
        var root = FindRepositoryRoot();
        var signingCssRoot = Path.Combine(root, "src", "Tempo.Blazor.Signing", "wwwroot", "css");
        var signingComponentCssRoot = Path.Combine(signingCssRoot, "components");
        var coreCssRoot = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "css");
        var coreComponentCssRoot = Path.Combine(coreCssRoot, "components");
        var signingEntryCss = Path.Combine(signingCssRoot, "tempo-blazor-signing.css");
        var coreEntryCss = Path.Combine(coreCssRoot, "tempo-blazor.css");
        var signingJs = Path.Combine(root, "src", "Tempo.Blazor.Signing", "wwwroot", "js", "pdf-template-designer.js");
        var oldCoreSigningJs = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "pdf-template-designer.js");
        var coreSignatureJs = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "signature-capture.js");
        var signingProject = Path.Combine(root, "src", "Tempo.Blazor.Signing", "Tempo.Blazor.Signing.csproj");

        File.Exists(signingEntryCss).Should().BeTrue();
        File.Exists(signingJs).Should().BeTrue();
        File.Exists(oldCoreSigningJs).Should().BeFalse();
        File.Exists(coreSignatureJs).Should().BeTrue();

        var signingImports = File.ReadAllText(signingEntryCss);
        var coreImports = File.ReadAllText(coreEntryCss);

        foreach (var cssFile in SigningCssFiles)
        {
            File.Exists(Path.Combine(signingComponentCssRoot, cssFile)).Should().BeTrue(cssFile);
            File.Exists(Path.Combine(coreComponentCssRoot, cssFile)).Should().BeFalse(cssFile);
            signingImports.Should().Contain($"@import \"components/{cssFile}\";");
            coreImports.Should().NotContain($"@import \"components/{cssFile}\";");
        }

        File.Exists(Path.Combine(coreComponentCssRoot, "_signature-capture.css")).Should().BeTrue();
        typeof(TmSignatureCapture).Assembly.GetName().Name.Should().Be("Tempo.Blazor");

        var project = File.ReadAllText(signingProject);
        project.Should().Contain("<Project Sdk=\"Microsoft.NET.Sdk.Razor\">");
        project.Should().Contain("<PackageId>Tempo.Blazor.Signing</PackageId>");
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
