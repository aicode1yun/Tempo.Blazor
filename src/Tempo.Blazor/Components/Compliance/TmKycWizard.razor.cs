using System.Globalization;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Components.Compliance;

/// <summary>
/// Step-by-step compliance/identification wizard (KYC/AML style, domain neutral):
/// subject identity (person or company), documents, addresses, ownership structure
/// tree (company subjects), declarations and a final review. Per-step validation is
/// pluggable through <see cref="IKycStepValidator"/>; drafts persist and submit
/// through <see cref="IKycProvider"/>.
/// </summary>
public partial class TmKycWizard : TmComponentBase
{
    // ── Parameters ───────────────────────────────────────────────────────────

    /// <summary>Persistence backend for drafts and the final submission. Required.</summary>
    [Parameter, EditorRequired] public IKycProvider Provider { get; set; } = default!;

    /// <summary>
    /// Per-step validator. When null, step navigation is not gated
    /// (use e.g. KycStepFluentValidator from Tempo.Blazor.FluentValidation).
    /// </summary>
    [Parameter] public IKycStepValidator? StepValidator { get; set; }

    /// <summary>Identifier of an existing draft to resume. Null starts a new draft.</summary>
    [Parameter] public string? DraftId { get; set; }

    /// <summary>Subject kind preselected for new drafts. Default is Person.</summary>
    [Parameter] public KycSubjectKind InitialSubjectKind { get; set; } = KycSubjectKind.Person;

    /// <summary>Callback invoked after a successful submission.</summary>
    [Parameter] public EventCallback<KycSubmissionResult> OnSubmitted { get; set; }

    /// <summary>Callback invoked after the draft was saved, with a snapshot of the draft.</summary>
    [Parameter] public EventCallback<KycDraft> OnDraftSaved { get; set; }

    /// <summary>Callback invoked when the active step changes.</summary>
    [Parameter] public EventCallback<KycWizardStep> OnStepChanged { get; set; }

    /// <summary>Additional CSS classes for the root element.</summary>
    [Parameter] public string? Class { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    // Radio group names must be unique per wizard instance; two wizards on one page
    // would otherwise form a single browser-level radio group.
    private readonly string _radioScope = Guid.NewGuid().ToString("N")[..8];

    private KycDraft _draft = new();
    private int _activeIndex;
    private List<KycValidationError> _errors = [];
    private KycSubmissionResult? _submissionResult;
    private DateTimeOffset? _draftSavedAt;
    private bool _busy;
    private IKycProvider? _loadedProvider;
    private string? _loadedDraftId;

    private IReadOnlyList<KycWizardStep> Steps
        => _draft.SubjectKind == KycSubjectKind.Company
            ? [KycWizardStep.Subject, KycWizardStep.Documents, KycWizardStep.Addresses, KycWizardStep.Ownership, KycWizardStep.Declarations, KycWizardStep.Review]
            : [KycWizardStep.Subject, KycWizardStep.Documents, KycWizardStep.Addresses, KycWizardStep.Declarations, KycWizardStep.Review];

    private KycWizardStep CurrentStep => Steps[Math.Min(_activeIndex, Steps.Count - 1)];

    private IReadOnlyList<IStepItem> StepItems
        => Steps.Select(IStepItem (s) => new KycStepItem(s.ToString(), StepLabel(s))).ToList();

    private sealed record KycStepItem(string Id, string Label) : IStepItem
    {
        public string? Description => null;

        public string? Icon => null;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (ReferenceEquals(Provider, _loadedProvider) && string.Equals(DraftId, _loadedDraftId, StringComparison.Ordinal))
        {
            return;
        }

        _loadedProvider = Provider;
        _loadedDraftId = DraftId;
        _activeIndex = 0;
        _errors = [];
        _submissionResult = null;
        _draftSavedAt = null;

        KycDraft? loaded = null;
        if (!string.IsNullOrEmpty(DraftId))
        {
            try
            {
                loaded = await Provider.LoadDraftAsync(DraftId);
            }
            catch
            {
                loaded = null;
            }
        }

        _draft = loaded ?? new KycDraft { SubjectKind = InitialSubjectKind };
        if (!string.IsNullOrEmpty(DraftId) && loaded is null)
        {
            _draft.Id = DraftId;
        }
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    private async Task NextAsync()
    {
        if (_activeIndex >= Steps.Count - 1)
        {
            return;
        }

        if (!await ValidateCurrentStepAsync())
        {
            return;
        }

        _activeIndex++;
        await OnStepChanged.InvokeAsync(CurrentStep);
    }

    private async Task BackAsync()
    {
        if (_activeIndex == 0)
        {
            return;
        }

        _errors = [];
        _activeIndex--;
        await OnStepChanged.InvokeAsync(CurrentStep);
    }

    private async Task HandleStepClickAsync(int index)
    {
        if (index == _activeIndex || index < 0 || index >= Steps.Count)
        {
            return;
        }

        _errors = [];
        _activeIndex = index;
        await OnStepChanged.InvokeAsync(CurrentStep);
    }

    private async Task<bool> ValidateCurrentStepAsync(KycWizardStep? step = null)
    {
        if (StepValidator is null)
        {
            _errors = [];
            return true;
        }

        _errors = [.. await StepValidator.ValidateAsync(step ?? CurrentStep, _draft)];
        return _errors.Count == 0;
    }

    // ── Persistence ──────────────────────────────────────────────────────────

    private async Task SaveDraftAsync()
    {
        _busy = true;
        try
        {
            _draft.UpdatedAt = DateTimeOffset.UtcNow;
            await Provider.SaveDraftAsync(_draft.Clone());
            _draftSavedAt = _draft.UpdatedAt;
            await OnDraftSaved.InvokeAsync(_draft.Clone());
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task SubmitAsync()
    {
        if (!await ValidateCurrentStepAsync(KycWizardStep.Review))
        {
            return;
        }

        _busy = true;
        try
        {
            var result = await Provider.SubmitAsync(_draft.Clone());
            _submissionResult = result;
            if (result.Success)
            {
                await OnSubmitted.InvokeAsync(result);
            }
            else
            {
                _errors = [.. result.Errors];
            }
        }
        finally
        {
            _busy = false;
        }
    }

    // ── Editing helpers ──────────────────────────────────────────────────────

    private void SetSubjectKind(KycSubjectKind kind)
    {
        _draft.SubjectKind = kind;
        // The step set changes with the kind; the subject step is always index 0.
        _activeIndex = 0;
    }

    private static void AddOwner(KycOwnershipNode parent) => parent.Children.Add(new KycOwnershipNode());

    private static IEnumerable<KycOwnershipNode> FlattenOwners(KycOwnershipNode root)
    {
        foreach (var child in root.Children)
        {
            yield return child;
            foreach (var nested in FlattenOwners(child))
            {
                yield return nested;
            }
        }
    }

    private string? FieldErrorKey(string fieldPath)
        => _errors.FirstOrDefault(e => string.Equals(e.FieldPath, fieldPath, StringComparison.Ordinal))?.MessageKey;

    private static string Text(ChangeEventArgs e) => e.Value?.ToString() ?? string.Empty;

    private static string? NullableText(ChangeEventArgs e)
    {
        var text = e.Value?.ToString();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static DateOnly? ParseDate(ChangeEventArgs e)
        => DateOnly.TryParse(e.Value?.ToString(), CultureInfo.InvariantCulture, out var date) ? date : null;

    private static decimal ParseDecimal(ChangeEventArgs e)
        => decimal.TryParse(e.Value?.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m;

    private static TEnum ParseEnum<TEnum>(ChangeEventArgs e, TEnum fallback) where TEnum : struct
        => Enum.TryParse<TEnum>(e.Value?.ToString(), out var parsed) ? parsed : fallback;

    private static string? DateValue(DateOnly? date)
        => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string FormatDate(DateOnly date)
        => date.ToString("d", CultureInfo.CurrentCulture);

    private string KindRadioName => $"tm-kyc-kind-{_radioScope}";

    private string PepRadioName => $"tm-kyc-pep-{_radioScope}";

    private string StepLabel(KycWizardStep step)
        => step switch
        {
            KycWizardStep.Subject => Loc["TmKycWizard_Step_Subject"],
            KycWizardStep.Documents => Loc["TmKycWizard_Step_Documents"],
            KycWizardStep.Addresses => Loc["TmKycWizard_Step_Addresses"],
            KycWizardStep.Ownership => Loc["TmKycWizard_Step_Ownership"],
            KycWizardStep.Declarations => Loc["TmKycWizard_Step_Declarations"],
            _ => Loc["TmKycWizard_Step_Review"]
        };

    private string DocumentKindLabel(KycDocumentKind kind)
        => kind switch
        {
            KycDocumentKind.NationalId => Loc["TmKycWizard_DocKind_NationalId"],
            KycDocumentKind.Passport => Loc["TmKycWizard_DocKind_Passport"],
            KycDocumentKind.DrivingLicense => Loc["TmKycWizard_DocKind_DrivingLicense"],
            KycDocumentKind.ResidencePermit => Loc["TmKycWizard_DocKind_ResidencePermit"],
            KycDocumentKind.RegisterExtract => Loc["TmKycWizard_DocKind_RegisterExtract"],
            _ => Loc["TmKycWizard_DocKind_Other"]
        };

    private string AddressKindLabel(KycAddressKind kind)
        => kind switch
        {
            KycAddressKind.Permanent => Loc["TmKycWizard_AddrKind_Permanent"],
            KycAddressKind.Mailing => Loc["TmKycWizard_AddrKind_Mailing"],
            KycAddressKind.Registered => Loc["TmKycWizard_AddrKind_Registered"],
            _ => Loc["TmKycWizard_AddrKind_Business"]
        };
}
