# PR-gate smoke lane: fast, deterministic E2E subset (< 20 min including host startup).
# Runs every test marked [TestCategory("Smoke")]. Exhaustive coverage lives in the
# nightly full lane (scripts/run-e2e-full.ps1). See docs/e2e-test-lanes.md.
param(
    [string]$ExtraFilter = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj'

$filter = 'TestCategory=Smoke'
if ($ExtraFilter) { $filter = "($filter)&($ExtraFilter)" }

# Traces cost ~800 MB per full run; the smoke lane keeps them on failure only
# unless the caller overrides TM_E2E_TRACE_ON_FAILURE explicitly.
dotnet test $project --filter $filter --logger 'trx;LogFileName=e2e-smoke.trx' --logger 'console;verbosity=normal'
exit $LASTEXITCODE
