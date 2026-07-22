using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Dtos;
using Tempo.ReportServer.Api.Rendering;
using Tempo.ReportServer.Api.Storage;

namespace Tempo.ReportServer.Api.Tests.Rendering;

/// <summary>
/// Operational-limit specification for <see cref="ReportRenderExecutor"/>: page quota, output-size cap,
/// timeout, and bounded concurrency each map to a distinct outcome without executing unbounded work.
/// </summary>
public sealed class ReportRenderExecutorTests
{
    [Fact]
    public async Task Execute_WithinLimits_ReturnsSucceeded()
    {
        var executor = CreateExecutor(new ReportServerQuotaOptions());
        var renderer = new FakeRenderer(Result(pageCount: 2, bytes: 128));

        var result = await executor.ExecuteAsync(renderer, Report(), Request(), Context());

        result.Outcome.Should().Be(ReportRenderOutcome.Succeeded);
        result.Result.Should().NotBeNull();
    }

    [Fact]
    public async Task Execute_ExceedingPageQuota_ReturnsPageQuotaExceeded()
    {
        var executor = CreateExecutor(new ReportServerQuotaOptions { MaxSynchronousPages = 1 });
        var renderer = new FakeRenderer(Result(pageCount: 5, bytes: 64));

        var result = await executor.ExecuteAsync(renderer, Report(), Request(), Context());

        result.Outcome.Should().Be(ReportRenderOutcome.PageQuotaExceeded);
        result.Result.Should().BeNull();
    }

    [Fact]
    public async Task Execute_ExceedingOutputSize_ReturnsOutputTooLarge()
    {
        var executor = CreateExecutor(new ReportServerQuotaOptions { MaxOutputBytes = 16 });
        var renderer = new FakeRenderer(Result(pageCount: 1, bytes: 1024));

        var result = await executor.ExecuteAsync(renderer, Report(), Request(), Context());

        result.Outcome.Should().Be(ReportRenderOutcome.OutputTooLarge);
    }

    [Fact]
    public async Task Execute_ExceedingTimeout_ReturnsTimedOut()
    {
        var executor = CreateExecutor(new ReportServerQuotaOptions { Timeout = TimeSpan.FromMilliseconds(50) });
        var renderer = new FakeRenderer(async token =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            return Result(1, 1);
        });

        var result = await executor.ExecuteAsync(renderer, Report(), Request(), Context());

        result.Outcome.Should().Be(ReportRenderOutcome.TimedOut);
    }

    [Fact]
    public async Task Execute_WhenQueueFull_ReturnsOverloaded()
    {
        var executor = CreateExecutor(new ReportServerQuotaOptions
        {
            MaxConcurrentRenders = 1,
            MaxRenderQueueLength = 0,
        });
        var entered = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var blocking = new FakeRenderer(async _ =>
        {
            entered.SetResult();
            await release.Task;
            return Result(1, 1);
        });

        var first = executor.ExecuteAsync(blocking, Report(), Request(), Context());
        await entered.Task; // the first render now owns the only slot

        var second = await executor.ExecuteAsync(new FakeRenderer(Result(1, 1)), Report(), Request(), Context());
        second.Outcome.Should().Be(ReportRenderOutcome.Overloaded);

        release.SetResult();
        (await first).Outcome.Should().Be(ReportRenderOutcome.Succeeded);
    }

    private static ReportRenderExecutor CreateExecutor(ReportServerQuotaOptions options)
        => new(Options.Create(options), new ReportRenderMetrics(), NullLogger<ReportRenderExecutor>.Instance);

    private static RenderReportResultDto Result(int pageCount, int bytes)
        => new()
        {
            TenantId = "tenant-a",
            ReportId = "report-1",
            Format = ReportRenderFormat.Pdf,
            ContentType = "application/pdf",
            FileName = "r.pdf",
            Bytes = new byte[bytes],
            PageCount = pageCount,
        };

    private static ReportDetailDto Report() => new() { TenantId = "tenant-a", ReportId = "report-1", Name = "R" };

    private static RenderReportRequestDto Request()
        => new() { TenantId = "tenant-a", ReportId = "report-1", Format = ReportRenderFormat.Pdf, CultureName = "en-US" };

    private static ReportExecutionContext Context() => new("tenant-a", "user-1", "en-US");

    private sealed class FakeRenderer : IReportServerRenderer
    {
        private readonly Func<CancellationToken, Task<RenderReportResultDto>> _render;

        public FakeRenderer(RenderReportResultDto result) => _render = _ => Task.FromResult(result);

        public FakeRenderer(Func<CancellationToken, Task<RenderReportResultDto>> render) => _render = render;

        public Task<IReadOnlyList<ReportParameterMetadataDto>> GetParametersAsync(ReportDetailDto report, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ReportParameterMetadataDto>>([]);

        public Task<RenderReportResultDto> RenderAsync(
            ReportDetailDto report,
            RenderReportRequestDto request,
            ReportExecutionContext context,
            CancellationToken cancellationToken = default)
            => _render(cancellationToken);
    }
}
