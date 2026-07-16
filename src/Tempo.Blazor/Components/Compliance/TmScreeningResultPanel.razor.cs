using System.Globalization;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Compliance;

/// <summary>
/// Panel showing the results of compliance screening checks for one subject
/// (category, severity, source, time, confidence) with a reviewer workflow to
/// confirm a finding as a true hit or dismiss it as a false positive through
/// <see cref="IScreeningProvider"/>.
/// </summary>
public partial class TmScreeningResultPanel : TmComponentBase
{
    // ── Parameters ───────────────────────────────────────────────────────────

    /// <summary>Data source of the findings and the resolution workflow. Required.</summary>
    [Parameter, EditorRequired] public IScreeningProvider Provider { get; set; } = default!;

    /// <summary>Identifier of the screened subject. Required.</summary>
    [Parameter, EditorRequired] public string SubjectId { get; set; } = string.Empty;

    /// <summary>Hides the confirm/dismiss actions when true. Default is false.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Reviewer identity recorded with resolutions.</summary>
    [Parameter] public string? CurrentUserName { get; set; }

    /// <summary>Whether the confidence indicator is shown. Default is true.</summary>
    [Parameter] public bool ShowConfidence { get; set; } = true;

    /// <summary>Callback invoked after a finding was confirmed or dismissed.</summary>
    [Parameter] public EventCallback<ScreeningFinding> OnFindingResolved { get; set; }

    /// <summary>Additional CSS classes for the root element.</summary>
    [Parameter] public string? Class { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private IReadOnlyList<ScreeningFinding> _findings = [];
    private bool _loading;
    private bool _busy;
    private string? _resolvingId;
    private ScreeningFindingStatus _resolvingStatus;
    private string _resolutionNote = string.Empty;
    private IScreeningProvider? _loadedProvider;
    private string? _loadedSubjectId;

    private int PendingCount => _findings.Count(f => f.Status == ScreeningFindingStatus.Pending);

    private IEnumerable<ScreeningFinding> OrderedFindings
        => _findings
            .OrderBy(f => f.Status == ScreeningFindingStatus.Pending ? 0 : 1)
            .ThenByDescending(f => f.Severity)
            .ThenByDescending(f => f.OccurredAt);

    // ── Lifecycle ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (ReferenceEquals(Provider, _loadedProvider) && string.Equals(SubjectId, _loadedSubjectId, StringComparison.Ordinal))
        {
            return;
        }

        _loadedProvider = Provider;
        _loadedSubjectId = SubjectId;
        // A resolution form open for the previous subject must not survive the switch.
        CancelResolution();
        await RefreshAsync();
    }

    /// <summary>Reloads the findings from the provider.</summary>
    public async Task RefreshAsync()
    {
        _loading = true;
        try
        {
            _findings = await Provider.GetFindingsAsync(SubjectId);
        }
        catch
        {
            _findings = [];
        }
        finally
        {
            _loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    // ── Resolution workflow ──────────────────────────────────────────────────

    private void BeginResolution(ScreeningFinding finding, ScreeningFindingStatus status)
    {
        _resolvingId = finding.Id;
        _resolvingStatus = status;
        _resolutionNote = string.Empty;
    }

    private void CancelResolution()
    {
        _resolvingId = null;
        _resolutionNote = string.Empty;
    }

    private async Task ResolveAsync(ScreeningFinding finding)
    {
        _busy = true;
        try
        {
            var resolved = await Provider.ResolveAsync(new ScreeningResolutionRequest
            {
                FindingId = finding.Id,
                Status = _resolvingStatus,
                Note = string.IsNullOrWhiteSpace(_resolutionNote) ? null : _resolutionNote,
                ResolvedBy = CurrentUserName
            });

            _resolvingId = null;
            _resolutionNote = string.Empty;
            await RefreshAsync();
            await OnFindingResolved.InvokeAsync(resolved);
        }
        catch
        {
            // The provider rejected the resolution; keep the form open so the reviewer can retry.
        }
        finally
        {
            _busy = false;
        }
    }

    // ── Display helpers ──────────────────────────────────────────────────────

    private static string SeverityModifier(ScreeningSeverity severity) => severity.ToString().ToLowerInvariant();

    private static string StatusModifier(ScreeningFindingStatus status) => status.ToString().ToLowerInvariant();

    private static int ConfidencePercent(double confidence)
        => (int)Math.Round(Math.Clamp(confidence, 0d, 1d) * 100d);

    private string ConfidenceTitle(double confidence)
        => string.Format(Loc["TmScreeningResultPanel_Confidence"], ConfidencePercent(confidence));

    private string SeverityLabel(ScreeningSeverity severity)
        => severity switch
        {
            ScreeningSeverity.Info => Loc["TmScreeningResultPanel_SeverityInfo"],
            ScreeningSeverity.Low => Loc["TmScreeningResultPanel_SeverityLow"],
            ScreeningSeverity.Medium => Loc["TmScreeningResultPanel_SeverityMedium"],
            ScreeningSeverity.High => Loc["TmScreeningResultPanel_SeverityHigh"],
            _ => Loc["TmScreeningResultPanel_SeverityCritical"]
        };

    private string StatusLabel(ScreeningFindingStatus status)
        => status switch
        {
            ScreeningFindingStatus.Confirmed => Loc["TmScreeningResultPanel_StatusConfirmed"],
            ScreeningFindingStatus.Dismissed => Loc["TmScreeningResultPanel_StatusDismissed"],
            _ => Loc["TmScreeningResultPanel_StatusPending"]
        };

    private static string FormatTime(DateTimeOffset time)
        => time.LocalDateTime.ToString("g", CultureInfo.CurrentCulture);
}
