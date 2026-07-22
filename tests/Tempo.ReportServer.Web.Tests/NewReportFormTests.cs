using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Reporting.Abstractions.Definitions;
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
        var cut = Render<NewReportForm>(parameters => parameters
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
        var cut = Render<NewReportForm>(parameters => parameters
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
        var cut = Render<NewReportForm>(parameters => parameters
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
        var cut = Render<NewReportForm>(parameters => parameters
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
        var cut = Render<NewReportForm>(parameters => parameters
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

    /// <summary>A minimal but valid SSRS 2016 RDL: a page setup plus one textbox in the body.</summary>
    private const string SampleRdl = """
        <?xml version="1.0" encoding="utf-8"?>
        <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
          <PageHeight>11in</PageHeight>
          <PageWidth>8.5in</PageWidth>
          <LeftMargin>1in</LeftMargin>
          <RightMargin>1in</RightMargin>
          <TopMargin>1in</TopMargin>
          <BottomMargin>1in</BottomMargin>
          <Body>
            <ReportItems>
              <Textbox Name="Heading">
                <Paragraphs><Paragraph><TextRuns><TextRun><Value>Legacy Sales</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                <Top>0.25in</Top><Left>0.5in</Left><Height>0.4in</Height><Width>4in</Width>
              </Textbox>
              <Subreport Name="Unsupported"><ReportName>Detail</ReportName>
                <Top>1in</Top><Left>0.5in</Left><Height>1in</Height><Width>4in</Width>
              </Subreport>
            </ReportItems>
          </Body>
        </Report>
        """;

    [Fact]
    public async Task Rdl_WithValidDocument_ImportsAndCallsCreateReportWithMappedDefinition()
    {
        // Drive OnSubmit through the SAME typed client the catalog page uses, so this proves the RDL path
        // reaches CreateReportAsync with the mapped definition rather than just raising a callback.
        var client = (FakeTempoReportServerClient)Services.GetRequiredService<ITempoReportServerClient>();
        var cut = Render<NewReportForm>(parameters => parameters
            .Add(component => component.TenantId, "northwind")
            .Add(component => component.Folders, Folders)
            .Add(component => component.OnSubmit, EventCallback.Factory.Create<CreateReportRequestDto>(
                this, async dto => await client.CreateReportAsync(dto))));

        cut.Find("[data-testid='new-report-name']").Input("Legacy Sales");
        cut.Find("[data-testid='new-report-source-rdl']").Change(true);
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText(SampleRdl, "legacy.rdl"));

        // The skipped <Subreport> must be surfaced as a non-fatal warning, never dropped silently.
        cut.Find("[data-testid='new-report-rdl-ok']").Should().NotBeNull();
        cut.Find("[data-testid='new-report-rdl-warnings']").Should().NotBeNull();

        cut.Find("[data-testid='new-report-submit']").Click();
        await Task.Yield();

        var request = client.LastCreateReportRequest;
        request.Should().NotBeNull();
        request!.Name.Should().Be("Legacy Sales");

        var definition = ReportDefinitionJsonSerializer.Deserialize(request.DefinitionJson);
        definition.Name.Should().Be("Legacy Sales");
        definition.PageSetup.PageSize.Width.Should().BeApproximately(612, 0.5);
        var textBox = definition.Bands.Detail!.Elements.OfType<ReportTextBoxElement>().Single();
        textBox.Id.Should().Be("Heading");
        textBox.Text.Should().Be("Legacy Sales");
    }

    [Fact]
    public void Rdl_WithMalformedDocument_ShowsInlineError_AndBlocksSubmit()
    {
        CreateReportRequestDto? captured = null;
        var cut = Render<NewReportForm>(parameters => parameters
            .Add(component => component.TenantId, "northwind")
            .Add(component => component.Folders, Folders)
            .Add(component => component.OnSubmit, EventCallback.Factory.Create<CreateReportRequestDto>(this, dto => captured = dto)));

        cut.Find("[data-testid='new-report-name']").Input("Broken");
        cut.Find("[data-testid='new-report-source-rdl']").Change(true);
        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("<Report><Body><Textbox></Body></Report>", "broken.rdl"));

        // A malformed RDL must surface inline (no exception, no 500) and must not produce a create request.
        cut.Find("[data-testid='new-report-file-error']").Should().NotBeNull();
        cut.FindAll("[data-testid='new-report-rdl-ok']").Should().BeEmpty();

        cut.Find("[data-testid='new-report-submit']").Click();
        captured.Should().BeNull();
    }

    [Fact]
    public void Upload_WithValidDefinition_RaisesRequestWithUploadedJson()
    {
        CreateReportRequestDto? captured = null;
        var cut = Render<NewReportForm>(parameters => parameters
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
