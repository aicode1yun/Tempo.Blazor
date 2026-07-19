using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Abstractions.Dtos;
using Tempo.Reporting.Abstractions.Serialization;
using Xunit.Abstractions;

namespace Tempo.ReportServer.Api.Tests.Rendering;

/// <summary>
/// End-to-end HTTP load harness (Fáze 14 / B4). Unlike the in-process F7 harness, this launches the API
/// as a real deployed <b>Kestrel</b> host (the built host exe) listening on <c>https://localhost:7001</c>,
/// backed by <b>SQL Server</b>, and fires many concurrent <c>POST /api/render</c> requests over real HTTP.
/// It measures throughput and p50/p95/p99 latency and asserts a defensible bound (p95 &lt; 5s, no failures).
///
/// Skipped by default (opt in with <c>REPORTSERVER_HTTP_LOADTEST=1</c>) so it never runs in the normal
/// suite or unattended CI. Authentication uses the dev scheme (Keycloak-free) so the run measures the
/// render/HTTP/DB path, not the identity provider. The host process is always killed on completion.
/// </summary>
public sealed class HttpKestrelLoadHarness
{
    private const string BaseUrl = "https://localhost:7001";
    private const string TenantId = "load-tenant";
    private const int TotalRequests = 200;
    private const int MaxInFlight = 50;

    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("REPORTSERVER_LOAD_CONNECTION")
        ?? "Server=localhost\\SQLEXPRESS;Database=TempoReportServerLoadTest;Integrated Security=true;TrustServerCertificate=true;";

    private readonly ITestOutputHelper _output;

    public HttpKestrelLoadHarness(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task ConcurrentHttpRenders_AgainstDeployedKestrelOnMsSql_MeetLatencyTarget()
    {
        if (Environment.GetEnvironmentVariable("REPORTSERVER_HTTP_LOADTEST") != "1")
        {
            _output.WriteLine("Skipped: set REPORTSERVER_HTTP_LOADTEST=1 to run the deployed-Kestrel HTTP load harness.");
            return;
        }

        using var host = await KestrelHost.StartAsync(ConnectionString, _output);
        using var handler = new HttpClientHandler
        {
            // Accept the ASP.NET Core localhost dev certificate for this loopback load run.
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) };

        await WaitForHealthAsync(client);
        var reportId = await SeedReportAsync(client);

        var request = new RenderReportRequestDto
        {
            TenantId = TenantId,
            ReportId = reportId,
            Format = ReportRenderFormat.Pdf,
            CultureName = "en-US",
        };

        // Warm up so JIT / first-connection / first-EF-query costs are excluded from the percentiles.
        (await client.PostAsJsonAsync("/api/render", request)).EnsureSuccessStatusCode();

        using var gate = new SemaphoreSlim(MaxInFlight);
        var overall = Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, TotalRequests).Select(async _ =>
        {
            await gate.WaitAsync();
            try
            {
                var sw = Stopwatch.StartNew();
                using var response = await client.PostAsJsonAsync("/api/render", request);
                sw.Stop();
                return (response.StatusCode, ElapsedMs: sw.Elapsed.TotalMilliseconds);
            }
            finally
            {
                gate.Release();
            }
        }).ToArray();
        var results = await Task.WhenAll(tasks);
        overall.Stop();

        var failures = results.Where(r => r.StatusCode != HttpStatusCode.OK).ToArray();
        var durations = results.Select(r => r.ElapsedMs).OrderBy(ms => ms).ToArray();
        var throughput = TotalRequests / overall.Elapsed.TotalSeconds;
        var p50 = Percentile(durations, 50);
        var p95 = Percentile(durations, 95);
        var p99 = Percentile(durations, 99);

        var summary =
            $"host=Kestrel({BaseUrl}) db=SqlServer requests={TotalRequests} inflight={MaxInFlight} " +
            $"wall={overall.Elapsed.TotalSeconds:0.00}s throughput={throughput:0.0}/s " +
            $"p50={p50:0}ms p95={p95:0}ms p99={p99:0}ms max={durations[^1]:0}ms failures={failures.Length}";
        _output.WriteLine(summary);
        await WriteReportAsync(summary, durations, throughput, p50, p95, p99, failures.Length, overall.Elapsed.TotalSeconds);

        failures.Should().BeEmpty("every concurrent HTTP render must complete without error");
        p95.Should().BeLessThan(5000, "p95 latency over HTTP must stay within the 5s target");
    }

    private static async Task WaitForHealthAsync(HttpClient client)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                using var response = await client.GetAsync("/health");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Host still starting.
            }

            await Task.Delay(500);
        }

        throw new TimeoutException("Kestrel host did not report healthy within the startup window.");
    }

    private static async Task<string> SeedReportAsync(HttpClient client)
    {
        var reportClient = new TempoReportServerClient(client);
        var folder = await reportClient.CreateFolderAsync(new CreateReportFolderRequestDto { TenantId = TenantId, Name = "Load" });
        var report = await reportClient.CreateReportAsync(new CreateReportRequestDto
        {
            TenantId = TenantId,
            FolderId = folder.FolderId,
            Name = "HTTP Load Register",
            DefinitionJson = DefinitionJson(),
        });
        return report.ReportId;
    }

    private static string DefinitionJson()
        => ReportDefinitionJsonSerializer.Serialize(new ReportDefinition
        {
            Id = "http-load-register",
            Name = "HTTP Load Register",
            DataSets = [new ReportDataSetDefinition { Name = "main" }],
            Bands = new ReportBandCollection
            {
                ReportHeader = new ReportBand
                {
                    Kind = ReportBandKind.ReportHeader,
                    Height = 60,
                    Elements =
                    [
                        new ReportTextBoxElement { Id = "title", X = 24, Y = 24, Width = 320, Height = 24, Text = "HTTP Load Register" },
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

    private static double Percentile(double[] sortedAscending, int percentile)
    {
        if (sortedAscending.Length == 0)
        {
            return 0;
        }

        var rank = (int)Math.Ceiling(percentile / 100.0 * sortedAscending.Length) - 1;
        return sortedAscending[Math.Clamp(rank, 0, sortedAscending.Length - 1)];
    }

    private static async Task WriteReportAsync(
        string summary,
        double[] durations,
        double throughput,
        double p50,
        double p95,
        double p99,
        int failures,
        double wallSeconds)
    {
        var repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "docs", "report-server-load.md");
        var timestamp = DateTimeOffset.UtcNow.ToString("u");
        var content =
            $"""
            # Report Server — end-to-end HTTP load test (Fáze 14 / B4)

            Deployed **Kestrel** host (`{BaseUrl}`, built host exe) backed by **SQL Server**, driven with
            concurrent `POST /api/render` over real HTTP. Authentication: dev scheme (Keycloak-free) so the
            measurement isolates the render/HTTP/DB path. Run gated by `REPORTSERVER_HTTP_LOADTEST=1`.

            _Last run: {timestamp}_

            ## Setup

            - Host: Kestrel, `{BaseUrl}` (HTTPS, ASP.NET Core dev certificate)
            - Database: SQL Server (`{ConnectionString}`)
            - Data provider: `EmptyReportDataProvider` (data-light report — measures HTTP + DB read + render + PDF)
            - Rendering quotas (env): MaxConcurrentRenders=8, MaxRenderQueueLength=500, Timeout=00:01:00
            - Requests: {TotalRequests} total, client-side concurrency cap {MaxInFlight} in flight
            - One warm-up render excluded from percentiles

            ## Results

            | metric | value |
            | --- | --- |
            | total requests | {TotalRequests} |
            | max in flight | {MaxInFlight} |
            | wall time | {wallSeconds:0.00} s |
            | throughput | {throughput:0.0} req/s |
            | p50 latency | {p50:0} ms |
            | p95 latency | {p95:0} ms |
            | p99 latency | {p99:0} ms |
            | max latency | {durations[^1]:0} ms |
            | failures (non-200) | {failures} |

            **Target:** p95 &lt; 5000 ms and 0 failures — **{(failures == 0 && p95 < 5000 ? "PASS" : "FAIL")}**.

            Raw summary line:

            ```
            {summary}
            ```

            ## Notes

            - F7's load test called the render executor in-process (p95 ≈ 1696 ms at 300 detail rows). This B4
              run adds the full Kestrel + TLS + HTTP/1.1 + SQL Server round-trip on every request; each render
              also reads the report definition/revision from SQL Server.
            - The host process is started and killed by the gated harness `HttpKestrelLoadHarness`.
            """;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }

    private sealed class KestrelHost : IDisposable
    {
        private readonly Process _process;

        private KestrelHost(Process process) => _process = process;

        public static async Task<KestrelHost> StartAsync(string connectionString, ITestOutputHelper output)
        {
            var repoRoot = FindRepoRoot();
            var exe = Path.Combine(
                repoRoot, "src", "Tempo.ReportServer.Api", "bin", "Debug", "net10.0", "Tempo.ReportServer.Api.exe");
            if (!File.Exists(exe))
            {
                throw new FileNotFoundException($"Built host exe not found at {exe}. Build the Api first.", exe);
            }

            var startInfo = new ProcessStartInfo(exe)
            {
                WorkingDirectory = Path.GetDirectoryName(exe)!,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            var env = startInfo.Environment;
            env["ASPNETCORE_ENVIRONMENT"] = "Development";
            env["ASPNETCORE_URLS"] = BaseUrl;
            env["ConnectionStrings__ReportServer"] = connectionString;
            env["Database__Provider"] = "SqlServer";
            env["Authentication__Dev__Enabled"] = "true";
            env["Authentication__Dev__TenantId"] = TenantId;
            env["Authentication__Jwt__Authority"] = string.Empty;
            env["Authentication__Jwt__RequireHttpsMetadata"] = "false";
            env["Scheduling__Enabled"] = "false";
            env["Database__Seed__Enabled"] = "false";
            env["Rendering__MaxConcurrentRenders"] = "8";
            env["Rendering__MaxRenderQueueLength"] = "500";
            env["Rendering__Timeout"] = "00:01:00";

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.WriteLine($"[host] {e.Data}"); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.WriteLine($"[host:err] {e.Data}"); };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await Task.Delay(500);
            return new KestrelHost(process);
        }

        public void Dispose()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                    _process.WaitForExit(10_000);
                }
            }
            catch (InvalidOperationException)
            {
                // Process already gone.
            }
            finally
            {
                _process.Dispose();
            }
        }
    }
}
