<#
.SYNOPSIS
    Runs the committed Report Server C# E2E lanes (Fáze 13).

.DESCRIPTION
    Two lanes, both gated behind env flags so a normal `dotnet test` never starts them:
      * CI DEMO lane      (default)     — TestCategory=ReportServerE2E,        gated on TM_RS_E2E.
                                          Self-hosts Api (SQLite, dev-auth) + Web (OIDC off). No Keycloak.
      * NIGHTLY full-stack (-FullStack) — TestCategory=ReportServerFullStack,  gated on TM_RS_FULLSTACK.
                                          Needs the live Keycloak service + SQL Server; launches smtp4dev.

    CI runs only the DEMO lane. Use -FullStack for the nightly/local full-stack run.

.EXAMPLE
    ./scripts/run-report-server-e2e.ps1               # CI demo lane
.EXAMPLE
    ./scripts/run-report-server-e2e.ps1 -FullStack    # nightly full-stack lane
#>
param(
    [switch]$FullStack
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'tests/Tempo.Blazor.E2E'

# Keep artifacts/NuGet off the tight C: drive when Z: is available (matches the lane's DB placement).
if (Test-Path 'Z:\') { $env:NUGET_PACKAGES = 'Z:\nuget-rs' }
$env:TM_E2E_SELF_HOST = 'false'          # skip the unrelated demo hosts
$env:TM_E2E_TRACE_ON_FAILURE = 'false'   # disk is tight

if ($FullStack) {
    $env:TM_RS_FULLSTACK = '1'
    $filter = 'TestCategory=ReportServerFullStack'
    Write-Host 'Running NIGHTLY full-stack lane (requires the live Keycloak service + SQL Server; smtp4dev is auto-launched).' -ForegroundColor Cyan
} else {
    $env:TM_RS_E2E = '1'
    $filter = 'TestCategory=ReportServerE2E'
    Write-Host 'Running CI DEMO lane (self-hosted Api+Web, no Keycloak).' -ForegroundColor Cyan
}

dotnet test $project -f net10.0 --filter $filter --property:NuGetAudit=false --property:TreatWarningsAsErrors=false
exit $LASTEXITCODE
