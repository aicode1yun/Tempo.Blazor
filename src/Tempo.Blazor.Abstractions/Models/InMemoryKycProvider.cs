namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// In-memory <see cref="IKycProvider"/> for demos and tests. Stores deep clones and
/// returns fresh instances on every read so callers exercise the same object-identity
/// semantics as a real persistence backend.
/// </summary>
public sealed class InMemoryKycProvider : IKycProvider
{
    private readonly object _gate = new();
    private readonly Dictionary<string, KycDraft> _drafts = new(StringComparer.Ordinal);
    private readonly List<KycDraft> _submissions = [];
    private int _submissionCounter;

    /// <summary>Snapshots of all submitted drafts, in submission order.</summary>
    public IReadOnlyList<KycDraft> Submissions
    {
        get
        {
            lock (_gate)
            {
                return _submissions.Select(d => d.Clone()).ToList();
            }
        }
    }

    /// <inheritdoc />
    public Task<KycDraft?> LoadDraftAsync(string draftId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_drafts.TryGetValue(draftId, out var draft) ? draft.Clone() : null);
        }
    }

    /// <inheritdoc />
    public Task SaveDraftAsync(KycDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        lock (_gate)
        {
            _drafts[draft.Id] = draft.Clone();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<KycSubmissionResult> SubmitAsync(KycDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        lock (_gate)
        {
            _submissions.Add(draft.Clone());
            _drafts.Remove(draft.Id);
            var submissionId = $"KYC-{++_submissionCounter:D6}";
            return Task.FromResult(new KycSubmissionResult { Success = true, SubmissionId = submissionId });
        }
    }
}
