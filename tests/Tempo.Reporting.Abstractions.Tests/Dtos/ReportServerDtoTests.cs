using System.Text.Json;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Abstractions.Dtos;
using Tempo.Reporting.Abstractions.Serialization;

namespace Tempo.Reporting.Abstractions.Tests.Dtos;

public sealed class ReportServerDtoTests
{
    [Fact]
    public void ReportServerDtos_DoNotExposeDomainDefinitionTypes()
    {
        var dtoTypes = new[]
        {
            typeof(ReportFolderDto),
            typeof(ReportSummaryDto),
            typeof(ReportDetailDto),
            typeof(ReportRevisionDto),
            typeof(CreateReportRequestDto),
            typeof(UpdateReportDefinitionRequestDto),
            typeof(RenderReportRequestDto),
            typeof(RenderJobDto),
        };

        dtoTypes.SelectMany(t => t.GetProperties())
            .Should().NotContain(p => p.PropertyType == typeof(ReportDefinition));
    }

    [Fact]
    public void RenderReportRequestDto_SerializesAsStableCamelCaseApiPayload()
    {
        var request = new RenderReportRequestDto
        {
            TenantId = "tenant-01",
            ReportId = "monthly-orders",
            RevisionId = "rev-7",
            Format = ReportRenderFormat.Snapshot,
            CultureName = "cs-CZ",
            Parameters =
            [
                new ReportParameterValueDto
                {
                    Name = "Region",
                    Values = ["EU", "NA"],
                },
            ],
        };

        var json = JsonSerializer.Serialize(request, ReportApiJsonSerializer.Options);

        json.Should().Be("{\"tenantId\":\"tenant-01\",\"reportId\":\"monthly-orders\",\"revisionId\":\"rev-7\",\"format\":\"snapshot\",\"cultureName\":\"cs-CZ\",\"parameters\":[{\"name\":\"Region\",\"values\":[\"EU\",\"NA\"]}]}");
    }
}
