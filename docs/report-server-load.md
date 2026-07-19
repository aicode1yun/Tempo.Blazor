# Report Server — end-to-end HTTP load test (Fáze 14 / B4)

Deployed **Kestrel** host (`https://localhost:7001`, built host exe) backed by **SQL Server**, driven with
concurrent `POST /api/render` over real HTTP. Authentication: dev scheme (Keycloak-free) so the
measurement isolates the render/HTTP/DB path. Run gated by `REPORTSERVER_HTTP_LOADTEST=1`.

_Last run: 2026-07-19 12:29:31Z_

## Setup

- Host: Kestrel, `https://localhost:7001` (HTTPS, ASP.NET Core dev certificate)
- Database: SQL Server (`Server=localhost\SQLEXPRESS;Database=TempoReportServerLoadTest;Integrated Security=true;TrustServerCertificate=true;`)
- Data provider: `EmptyReportDataProvider` (data-light report — measures HTTP + DB read + render + PDF)
- Rendering quotas (env): MaxConcurrentRenders=8, MaxRenderQueueLength=500, Timeout=00:01:00
- Requests: 200 total, client-side concurrency cap 50 in flight
- One warm-up render excluded from percentiles

## Results

| metric | value |
| --- | --- |
| total requests | 200 |
| max in flight | 50 |
| wall time | 3,01 s |
| throughput | 66,4 req/s |
| p50 latency | 559 ms |
| p95 latency | 1338 ms |
| p99 latency | 1601 ms |
| max latency | 1692 ms |
| failures (non-200) | 0 |

**Target:** p95 &lt; 5000 ms and 0 failures — **PASS**.

Raw summary line:

```
host=Kestrel(https://localhost:7001) db=SqlServer requests=200 inflight=50 wall=3,01s throughput=66,4/s p50=559ms p95=1338ms p99=1601ms max=1692ms failures=0
```

## Notes

- F7's load test called the render executor in-process (p95 ≈ 1696 ms at 300 detail rows). This B4
  run adds the full Kestrel + TLS + HTTP/1.1 + SQL Server round-trip on every request; each render
  also reads the report definition/revision from SQL Server.
- The host process is started and killed by the gated harness `HttpKestrelLoadHarness`.