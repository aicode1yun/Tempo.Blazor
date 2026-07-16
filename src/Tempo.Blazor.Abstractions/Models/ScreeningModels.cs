namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Severity of a screening finding.</summary>
public enum ScreeningSeverity
{
    /// <summary>Informational only.</summary>
    Info = 0,

    /// <summary>Low severity.</summary>
    Low = 1,

    /// <summary>Medium severity.</summary>
    Medium = 2,

    /// <summary>High severity.</summary>
    High = 3,

    /// <summary>Critical severity.</summary>
    Critical = 4
}

/// <summary>Review status of a screening finding.</summary>
public enum ScreeningFindingStatus
{
    /// <summary>Awaiting a reviewer decision.</summary>
    Pending = 0,

    /// <summary>Confirmed as a true hit.</summary>
    Confirmed = 1,

    /// <summary>Dismissed as a false positive.</summary>
    Dismissed = 2
}

/// <summary>One finding produced by a screening/check run against a subject.</summary>
public sealed class ScreeningFinding
{
    /// <summary>Stable identifier of the finding.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Identifier of the screened subject the finding belongs to.</summary>
    public string SubjectId { get; set; } = string.Empty;

    /// <summary>App-defined category of the check (e.g. "sanctions", "pep", "adverse-media").</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Short title of the finding (e.g. the matched name).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional longer description.</summary>
    public string? Description { get; set; }

    /// <summary>Source of the check (list, register, provider name).</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>When the finding was produced.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Match confidence in the 0–1 range.</summary>
    public double Confidence { get; set; }

    /// <summary>Severity of the finding.</summary>
    public ScreeningSeverity Severity { get; set; }

    /// <summary>Review status.</summary>
    public ScreeningFindingStatus Status { get; set; }

    /// <summary>Reviewer note recorded with the resolution.</summary>
    public string? ResolutionNote { get; set; }

    /// <summary>Who resolved the finding.</summary>
    public string? ResolvedBy { get; set; }

    /// <summary>When the finding was resolved.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>Creates a deep copy.</summary>
    public ScreeningFinding Clone() => (ScreeningFinding)MemberwiseClone();
}

/// <summary>Request to resolve (confirm or dismiss) a screening finding.</summary>
public sealed class ScreeningResolutionRequest
{
    /// <summary>Identifier of the finding being resolved.</summary>
    public string FindingId { get; set; } = string.Empty;

    /// <summary>Target status; must be <see cref="ScreeningFindingStatus.Confirmed"/> or <see cref="ScreeningFindingStatus.Dismissed"/>.</summary>
    public ScreeningFindingStatus Status { get; set; }

    /// <summary>Optional reviewer note.</summary>
    public string? Note { get; set; }

    /// <summary>Optional reviewer identity.</summary>
    public string? ResolvedBy { get; set; }
}

/// <summary>Data source of screening results and the resolution workflow.</summary>
public interface IScreeningProvider
{
    /// <summary>Returns all findings recorded for the subject.</summary>
    Task<IReadOnlyList<ScreeningFinding>> GetFindingsAsync(string subjectId, CancellationToken cancellationToken = default);

    /// <summary>Applies a reviewer decision to a finding and returns the updated finding.</summary>
    Task<ScreeningFinding> ResolveAsync(ScreeningResolutionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory <see cref="IScreeningProvider"/> for demos and tests. Returns clones on
/// every read so component-side mutations never leak into the stored findings.
/// </summary>
public sealed class InMemoryScreeningProvider : IScreeningProvider
{
    private readonly object _gate = new();
    private readonly List<ScreeningFinding> _findings;

    /// <summary>Creates the provider seeded with <paramref name="findings"/>.</summary>
    public InMemoryScreeningProvider(IEnumerable<ScreeningFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);
        _findings = findings.Select(f => f.Clone()).ToList();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ScreeningFinding>> GetFindingsAsync(string subjectId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<ScreeningFinding> result = _findings
                .Where(f => string.Equals(f.SubjectId, subjectId, StringComparison.Ordinal))
                .Select(f => f.Clone())
                .ToList();
            return Task.FromResult(result);
        }
    }

    /// <inheritdoc />
    public Task<ScreeningFinding> ResolveAsync(ScreeningResolutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Status == ScreeningFindingStatus.Pending)
        {
            throw new ArgumentException("A resolution must set Confirmed or Dismissed, not Pending.", nameof(request));
        }

        lock (_gate)
        {
            var finding = _findings.FirstOrDefault(f => string.Equals(f.Id, request.FindingId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"Screening finding '{request.FindingId}' was not found.");

            finding.Status = request.Status;
            finding.ResolutionNote = request.Note;
            finding.ResolvedBy = request.ResolvedBy;
            finding.ResolvedAt = DateTimeOffset.UtcNow;
            return Task.FromResult(finding.Clone());
        }
    }
}
