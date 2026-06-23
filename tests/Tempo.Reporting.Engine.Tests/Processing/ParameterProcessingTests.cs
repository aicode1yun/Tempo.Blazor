using DataFieldType = Tempo.Reporting.Abstractions.Data.ReportDataFieldType;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Processing;

namespace Tempo.Reporting.Engine.Tests.Processing;

public sealed class ParameterProcessingTests
{
    [Fact]
    public async Task ResolveAsync_ValidatesRequiredMultiplicityAndType()
    {
        var definition = new ReportDefinition
        {
            Parameters =
            [
                new ReportParameterDefinition { Name = "Country", DataType = ReportParameterType.String, Required = true },
                new ReportParameterDefinition { Name = "MinTotal", DataType = ReportParameterType.Number, Required = true },
                new ReportParameterDefinition { Name = "Tags", DataType = ReportParameterType.String, AllowMultipleValues = false },
            ],
        };
        var provider = new ParameterDataProvider();
        var context = new ReportExecutionContext("tenant", "user", "en-US");

        var missing = () => ReportParameterProcessor.ResolveAsync(
            definition,
            provider,
            new Dictionary<string, ReportParameterValue>(),
            context);
        await missing.Should().ThrowAsync<ReportProcessingException>()
            .Where(exception => exception.Code == "Parameters.Required");

        var invalidNumber = () => ReportParameterProcessor.ResolveAsync(
            definition,
            provider,
            new Dictionary<string, ReportParameterValue>
            {
                ["Country"] = ReportParameterValue.Scalar("CZ"),
                ["MinTotal"] = ReportParameterValue.Scalar("not-number"),
            },
            context);
        await invalidNumber.Should().ThrowAsync<ReportProcessingException>()
            .Where(exception => exception.Code == "Parameters.InvalidValue");

        var multi = () => ReportParameterProcessor.ResolveAsync(
            definition,
            provider,
            new Dictionary<string, ReportParameterValue>
            {
                ["Country"] = ReportParameterValue.Scalar("CZ"),
                ["MinTotal"] = ReportParameterValue.Scalar(10m),
                ["Tags"] = ReportParameterValue.Multiple(["a", "b"]),
            },
            context);
        await multi.Should().ThrowAsync<ReportProcessingException>()
            .Where(exception => exception.Code == "Parameters.MultiValueNotAllowed");
    }

    [Fact]
    public async Task ResolveAsync_EvaluatesDefaultsAndStaticAvailableValues()
    {
        var definition = new ReportDefinition
        {
            Parameters =
            [
                new ReportParameterDefinition
                {
                    Name = "Country",
                    DataType = ReportParameterType.List,
                    DefaultExpression = "=\"CZ\"",
                    AvailableValues = ReportParameterAvailableValues.Static(
                    [
                        new ReportParameterAvailableValue("CZ", "Czechia"),
                        new ReportParameterAvailableValue("SK", "Slovakia"),
                    ]),
                },
                new ReportParameterDefinition
                {
                    Name = "Greeting",
                    DataType = ReportParameterType.String,
                    DefaultExpression = "=\"Hello \" + Parameters.Country",
                },
            ],
        };

        var result = await ReportParameterProcessor.ResolveAsync(
            definition,
            new ParameterDataProvider(),
            new Dictionary<string, ReportParameterValue>(),
            new ReportExecutionContext("tenant", "user", "en-US"));

        result.Values["Country"].ScalarValue.Should().Be("CZ");
        result.Values["Greeting"].ScalarValue.Should().Be("Hello CZ");
        result.AvailableValues["Country"].Select(value => (value.Value, value.Label))
            .Should().Equal(("CZ", "Czechia"), ("SK", "Slovakia"));
    }

    [Fact]
    public async Task ResolveAsync_LoadsAvailableValuesFromDataSetAndPassesCascadingParameters()
    {
        var definition = new ReportDefinition
        {
            Parameters =
            [
                new ReportParameterDefinition
                {
                    Name = "Country",
                    DataType = ReportParameterType.String,
                    DefaultExpression = "=\"CZ\"",
                },
                new ReportParameterDefinition
                {
                    Name = "City",
                    DataType = ReportParameterType.List,
                    DefaultExpression = "=\"PRG\"",
                    AvailableValues = ReportParameterAvailableValues.FromDataSet("Cities", "Id", "Name"),
                },
            ],
            DataSets =
            [
                new ReportDataSetDefinition
                {
                    Name = "Cities",
                    Parameters =
                    [
                        new ReportDataSetParameterBinding("country", "=Parameters.Country"),
                    ],
                },
            ],
        };
        var provider = new ParameterDataProvider();

        var result = await ReportParameterProcessor.ResolveAsync(
            definition,
            provider,
            new Dictionary<string, ReportParameterValue>(),
            new ReportExecutionContext("tenant", "user", "en-US"));

        result.AvailableValues["City"].Select(value => (value.Value, value.Label))
            .Should().Equal(("PRG", "Praha"), ("BRQ", "Brno"));
        provider.LastParameters.Should().ContainKey("country");
        provider.LastParameters["country"].ScalarValue.Should().Be("CZ");
    }

    private sealed class ParameterDataProvider : IReportDataProvider
    {
        public IReadOnlyDictionary<string, ReportParameterValue> LastParameters { get; private set; }
            = new Dictionary<string, ReportParameterValue>();

        public Task<ReportDataSetResult> GetDataAsync(
            string dataSetName,
            ReportDataQuery query,
            IReadOnlyDictionary<string, ReportParameterValue> parameters,
            ReportExecutionContext context)
        {
            LastParameters = new Dictionary<string, ReportParameterValue>(parameters);
            IReadOnlyList<(string Id, string Name, string Country)> rows = dataSetName == "Cities"
                ? [("PRG", "Praha", "CZ"), ("BRQ", "Brno", "CZ"), ("BTS", "Bratislava", "SK")]
                : [];
            var country = parameters.TryGetValue("country", out var value)
                ? Convert.ToString(value.ScalarValue, System.Globalization.CultureInfo.InvariantCulture)
                : null;
            rows = rows.Where(row => country is null || row.Country == country).ToArray();

            return Task.FromResult(new ReportDataSetResult(
                [
                    new ReportDataColumn("Id", DataFieldType.String),
                    new ReportDataColumn("Name", DataFieldType.String),
                    new ReportDataColumn("Country", DataFieldType.String),
                ],
                Stream(rows)));
        }

        private static async IAsyncEnumerable<ReportDataRow> Stream(IEnumerable<(string Id, string Name, string Country)> rows)
        {
            foreach (var row in rows)
            {
                await Task.Yield();
                yield return new ReportDataRow(new Dictionary<string, object?>
                {
                    ["Id"] = row.Id,
                    ["Name"] = row.Name,
                    ["Country"] = row.Country,
                });
            }
        }
    }
}
