using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Tempo.Reporting.Abstractions.Dtos;
using Tempo.Reporting.Abstractions.Serialization;
using Tempo.ReportServer.Web.Components;
using Tempo.ReportServer.Web.Tests.Fixtures;

namespace Tempo.ReportServer.Web.Tests;

public sealed class NewReportFormTests : ReportServerWebTestBase
{
    private static readonly IReadOnlyList<ReportFolderDto> Folders =
    [
        new() { TenantId = "northwind", FolderId = "folder-finance", Name = "Finance", Path = "/Finance" },
    ];

    [Fact]
    public void Submit_WithMissingName_BlocksSubmit_AndShowsInlineError()
    {
        CreateReportRequestDto? captured = null;
        var cut = RenderComponent<NewReportForm>(parameters => parameters
            .Add(component => component.TenantId, "northwind")
            .Add(component => component.Folders, Folders)
            .Add(component => component.OnSubmit, EventCallback.Factory.Create<CreateReportRequestDto>(this, dto => captured = dto)));

        cut.Find("[data-testid='new-report-submit']").Click();

        cut.Find("[data-testid='new-report-name-error']").Should().NotBeNull();
        captured.Should().BeNull();
    }

    [Fact]
    public void Submit_WithNoFolderSelected_BlocksSubmit_AndShowsFolderError()
    {
        CreateReportRequestDto? captured = null;
        var cut = RenderComponent<NewReportForm>(parameters => parameters
            .Add(component => component.TenantId, "northwind")
            .Add(component => component.Folders, [])
            .Add(component => component.OnSubmit, EventCallback.Factory.Create<CreateReportRequestDto>(this, dto => captured = dto)));

        cut.Find("[data-testid='new-report-name']").Input("Quarter End");
        cut.Find("[data-testid='new-report-submit']").Click();

        cut.Find("[data-testid='new-report-folder-error']").Should().NotBeNull();
        captured.Should().BeNull();
    }

    [Fact]
    public void Submit_Blank_BuildsMinimalDefinition_AndRaisesRequest()
    {
        CreateReportRequestDto? captured = null;
        var cut = RenderComponent<NewReportForm>(parameters => parameters
            .Add(component => component.TenantId, "northwind")
            .Add(component => component.Folders, Folders)
            .Add(component => component.OnSubmit, EventCallback.Factory.Create<CreateReportRequestDto>(this, dto => captured = dto)));

        cut.Find("[data-testid='new-report-name']").Input("Quarter End");
        cut.Find("[data-testid='new-report-submit']").Click();

        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be("northwind");
        captured.FolderId.Should().Be("folder-finance");
        captured.Name.Should().Be("Quarter End");
        captured.DefinitionJson.Should().Contain("Quarter End");
    }

    [Fact]
    public void Submit_Blank_ProducesDefinitionJson_ThatRoundTripsThroughCanonicalSerializer()
    {
        // The blank definition must be serialized with the SAME canonical serializer the server reads it
        // back with; plain System.Text.Json does not round-trip (e.g. ReportPageSize.unit) and would 500
        // the server's GET /reports/{id}/parameters. This asserts the produced JSON deserializes cleanly.
        CreateReportRequestDto? captured = null;
        var cut = RenderComponent<NewReportForm>(parameters => parameters
            .Add(component => component.TenantId, "northwind")
            .Add(component => component.Folders, Folders)
            .Add(component => component.OnSubmit, EventCallback.Factory.Create<CreateReportRequestDto>(this, dto => captured = dto)));

        cut.Find("[data-testid='new-report-name']").Input("Quarter End");
        cut.Find("[data-testid='new-report-submit']").Click();

        captured.Should().NotBeNull();
        var roundTripped = ReportDefinitionJsonSerializer.Deserialize(captured!.DefinitionJson);
        roundTripped.Name.Should().Be("Quarter End");
    }

    [Fact]
    public void Upload_WithInvalidJson_ShowsInlineError_AndBlocksSubmit()
    {
        CreateReportRequestDto? captured = null;
        var cut = RenderComponent<NewReportForm>(parameters => parameters
            .Add(component => component.TenantId, "northwind")
            .Add(component => component.Folders, Folders)
            .Add(component => component.OnSubmit, EventCallback.Factory.Create<CreateReportRequestDto>(this, dto => captured = dto)));

        cut.Find("[data-testid='new-report-name']").Input("Uploaded");
        cut.Find("[data-testid='new-report-source-upload']").Change(true);
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("{ this is not valid", "definition.json"));

        cut.Find("[data-testid='new-report-file-error']").Should().NotBeNull();

        cut.Find("[data-testid='new-report-submit']").Click();
        captured.Should().BeNull();
    }

    [Fact]
    public void Upload_WithValidDefinition_RaisesRequestWithUploadedJson()
    {
        CreateReportRequestDto? captured = null;
        var cut = RenderComponent<NewReportForm>(parameters => parameters
            .Add(component => component.TenantId, "northwind")
            .Add(component => component.Folders, Folders)
            .Add(component => component.OnSubmit, EventCallback.Factory.Create<CreateReportRequestDto>(this, dto => captured = dto)));

        const string definition = "{\"schemaVersion\":1,\"name\":\"Imported\"}";
        cut.Find("[data-testid='new-report-name']").Input("Imported");
        cut.Find("[data-testid='new-report-source-upload']").Change(true);
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText(definition, "definition.json"));

        cut.Find("[data-testid='new-report-submit']").Click();

        captured.Should().NotBeNull();
        captured!.DefinitionJson.Should().Be(definition);
    }
}
