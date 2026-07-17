using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Abstractions.Dtos;
using Tempo.Reporting.Abstractions.Serialization;
using Tempo.ReportServer.Api.Rendering;
using Xunit.Abstractions;

namespace Tempo.ReportServer.Api.Tests.Rendering;

/// <summary>
/// Load harness: fires 50 concurrent renders of a medium multi-page report through the real render
/// endpoint (bounded-concurrency executor included) and reports latency percentiles. Skipped by
/// default so it never slows the normal suite; opt in with environment variable
/// <c>REPORTSERVER_LOADTEST=1</c>. It asserts every render succeeds (no failures, no OOM) and prints
/// p50/p95/max so a run can be recorded against a target.
/// </summary>
public sealed class RenderConcurrencyLoadHarness
{
    private const int ConcurrentRenders = 50;
    private const int DetailRows = 300;
    private const string TenantId = "load-tenant";

    private readonly ITestOutputHelper _output;

    public RenderConcurrencyLoadHarness(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task FiftyConcurrentRenders_AllSucceed_AndReportLatencies()
    {
        if (Environment.GetEnvironmentVariable("REPORTSERVER_LOADTEST") != "1")
        {
            _output.WriteLine("Skipped: set REPORTSERVER_LOADTEST=1 to run the render load harness.");
            return;
        }

        await using var app = await LoadTestApp.CreateAsync(DetailRows);
        var reportId = await CreateMediumReportAsync(app);

        var request = new RenderReportRequestDto
        {
            TenantId = TenantId,
            ReportId = reportId,
            Format = ReportRenderFormat.Pdf,
            CultureName = "en-US",
        };

        // Warm up a single render so JIT / first-touch costs are excluded from the percentiles.
        (await app.Client.PostAsJsonAsync("/api/render", request)).EnsureSuccessStatusCode();

        var overall = Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, ConcurrentRenders).Select(async _ =>
        {
            var sw = Stopwatch.StartNew();
            using var response = await app.Client.PostAsJsonAsync("/api/render", request);
            sw.Stop();
            return (response.StatusCode, ElapsedMs: sw.Elapsed.TotalMilliseconds);
        }).ToArray();
        var results = await Task.WhenAll(tasks);
        overall.Stop();

        var failures = results.Where(r => r.StatusCode != HttpStatusCode.OK).ToArray();
        var durations = results.Select(r => r.ElapsedMs).OrderBy(ms => ms).ToArray();
        var summary =
            $"renders={ConcurrentRenders} rows={DetailRows} wall={overall.Elapsed.TotalSeconds:0.00}s " +
            $"p50={Percentile(durations, 50):0}ms p95={Percentile(durations, 95):0}ms max={durations[^1]:0}ms " +
            $"failures={failures.Length}";
        _output.WriteLine(summary);
        File.WriteAllText(
            Path.Combine(Path.GetTempPath(), "reportserver-loadtest.txt"),
            summary + Environment.NewLine);

        failures.Should().BeEmpty("every concurrent render must complete without error");
    }

    private static double Percentile(double[] sortedAscending, int percentile)
    {
        if (sortedAscending.Length == 0)
        {
            return 0;
        }

        var rank = (int)Math.Ceiling(percentile / 100.0 * sortedAscending.Length) - 1;
        return sortedAscending[Math.Clamp(rank, 0, sortedAscending.Length - 1)];
    }

    private static async Task<string> CreateMediumReportAsync(LoadTestApp app)
    {
        var client = new TempoReportServerClient(app.Client);
        var folder = await client.CreateFolderAsync(new CreateReportFolderRequestDto { TenantId = TenantId, Name = "Load" });
        var report = await client.CreateReportAsync(new CreateReportRequestDto
        {
            TenantId = TenantId,
            FolderId = folder.FolderId,
            Name = "Medium Register",
            DefinitionJson = MediumDefinitionJson(),
        });
        return report.ReportId;
    }

    private static string MediumDefinitionJson()
        => ReportDefinitionJsonSerializer.Serialize(new ReportDefinition
        {
            Id = "medium-register",
            Name = "Medium Register",
            DataSets = [new ReportDataSetDefinition { Name = "main" }],
            Bands = new ReportBandCollection
            {
                ReportHeader = new ReportBand
                {
                    Kind = ReportBandKind.ReportHeader,
                    Height = 60,
                    Elements =
                    [
                        new ReportTextBoxElement { Id = "title", X = 24, Y = 24, Width = 320, Height = 24, Text = "Medium Register" },
                    ],
                },
                Detail = new ReportBand
                {
                    Kind = ReportBandKind.Detail,
                    Height = 24,
                    Elements =
                    [
                        new ReportTextBoxElement { Id = "row", X = 24, Y = 2, Width = 480, Height = 20, Text = "Detail line item" },
                    ],
                },
            },
        });

    private sealed class LoadTestApp : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly string _databasePath;

        private LoadTestApp(WebApplication app, string databasePath)
        {
            _app = app;
            _databasePath = databasePath;
            Client = app.GetTestClient();
        }

        public HttpClient Client { get; }

        public static async Task<LoadTestApp> CreateAsync(int detailRows)
        {
            // A file-backed SQLite database (not a single shared in-memory connection) so EF's
            // connection pool serves the 50 concurrent report reads with independent connections;
            // SQLite permits concurrent readers. The catalog is written once at setup.
            var databasePath = Path.Combine(Path.GetTempPath(), $"reportserver-loadtest-{Guid.NewGuid():N}.db");
            var connectionString = $"Data Source={databasePath}";
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddTempoReportServerApi(options => options.UseSqlite(connectionString));
            builder.Services.AddScoped<IReportDataProvider>(_ => new RowGeneratingDataProvider(detailRows));
            var app = builder.Build();
            app.UseTempoReportServerTenantContext();
            app.MapTempoReportServerApi();
            await app.Services.EnsureTempoReportServerDatabaseAsync();
            await app.StartAsync();
            return new LoadTestApp(app, databasePath);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.DisposeAsync();
            SqliteConnection.ClearAllPools();
            try
            {
                File.Delete(_databasePath);
            }
            catch (IOException)
            {
                // Best-effort cleanup of the temp database file.
            }
        }
    }

    private sealed class RowGeneratingDataProvider : IReportDataProvider
    {
        private readonly int _rows;

        public RowGeneratingDataProvider(int rows) => _rows = rows;

        public Task<ReportDataSetResult> GetDataAsync(
            string dataSetName,
            ReportDataQuery query,
            IReadOnlyDictionary<string, ReportParameterValue> parameters,
            ReportExecutionContext context)
            => Task.FromResult(new ReportDataSetResult([], Rows(_rows, context.CancellationToken)));

        private static async IAsyncEnumerable<ReportDataRow> Rows(
            int count,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            for (var i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new ReportDataRow(new Dictionary<string, object?> { ["Name"] = $"Row {i}", ["Value"] = i });
            }
        }
    }
}
