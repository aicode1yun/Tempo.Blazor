using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Autocomplete trigger kind used by the document editor.</summary>
public enum DocumentAutocompleteKind
{
    /// <summary>Template token trigger, for example <c>{{</c>.</summary>
    Token,

    /// <summary>User mention trigger, for example <c>@</c>.</summary>
    Mention,

    /// <summary>Tag trigger, for example <c>#</c>.</summary>
    Tag,

    /// <summary>Slash command trigger, for example <c>/</c>.</summary>
    SlashCommand
}

/// <summary>Descriptor for one autocomplete trigger.</summary>
public sealed class DocumentAutocompleteTrigger
{
    /// <summary>Stable trigger id.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Autocomplete kind served by this trigger.</summary>
    public DocumentAutocompleteKind Kind { get; init; }

    /// <summary>Text marker that opens the menu.</summary>
    public string Marker { get; init; } = string.Empty;

    /// <summary>Minimum query length required after the marker.</summary>
    public int MinimumCharacters { get; init; }

    /// <summary>Maximum number of items displayed in the menu.</summary>
    public int Limit { get; init; } = 8;

    /// <summary>Renderer key used by the menu item template.</summary>
    public string RendererKey { get; init; } = "default";

    /// <summary>Runtime marker type used by the JS-owned surface.</summary>
    public string MarkerType { get; init; } = "autocompleteQuery";

    /// <summary>Returns true when the supplied query is long enough to search.</summary>
    public bool CanSearch(string? query) => (query ?? string.Empty).Length >= MinimumCharacters;

    /// <summary>Creates the default token trigger.</summary>
    public static DocumentAutocompleteTrigger Token() => new()
    {
        Id = "token",
        Kind = DocumentAutocompleteKind.Token,
        Marker = "{{",
        MinimumCharacters = 0,
        Limit = 8,
        RendererKey = "token",
        MarkerType = "tokenQuery"
    };

    /// <summary>Creates the default mention trigger.</summary>
    public static DocumentAutocompleteTrigger Mention() => new()
    {
        Id = "mention",
        Kind = DocumentAutocompleteKind.Mention,
        Marker = "@",
        MinimumCharacters = 0,
        Limit = 8,
        RendererKey = "mention",
        MarkerType = "mentionQuery"
    };

    /// <summary>Creates the default tag trigger.</summary>
    public static DocumentAutocompleteTrigger Tag() => new()
    {
        Id = "tag",
        Kind = DocumentAutocompleteKind.Tag,
        Marker = "#",
        MinimumCharacters = 1,
        Limit = 8,
        RendererKey = "tag",
        MarkerType = "tagQuery"
    };

    /// <summary>Creates the default slash command trigger.</summary>
    public static DocumentAutocompleteTrigger SlashCommand() => new()
    {
        Id = "slash",
        Kind = DocumentAutocompleteKind.SlashCommand,
        Marker = "/",
        MinimumCharacters = 0,
        Limit = 8,
        RendererKey = "command",
        MarkerType = "slashQuery"
    };
}

/// <summary>Autocomplete item rendered by the document editor menu.</summary>
public sealed class DocumentAutocompleteItem
{
    /// <summary>Stable item id.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Main display label.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Optional secondary text.</summary>
    public string? Description { get; init; }

    /// <summary>Item kind.</summary>
    public DocumentAutocompleteKind Kind { get; init; }

    /// <summary>Optional icon name.</summary>
    public string? Icon { get; init; }

    /// <summary>Optional grouping text.</summary>
    public string? Group { get; init; }

    /// <summary>Raw value inserted or executed when selected.</summary>
    public string? Value { get; init; }

    /// <summary>Renderer key used by the menu item template.</summary>
    public string RendererKey { get; init; } = "default";

    /// <summary>Additional provider-specific metadata.</summary>
    public Dictionary<string, string?> Metadata { get; init; } = [];
}

/// <summary>Autocomplete search request.</summary>
public sealed class DocumentAutocompleteRequest
{
    /// <summary>Trigger that produced the request.</summary>
    public DocumentAutocompleteTrigger Trigger { get; init; } = DocumentAutocompleteTrigger.Token();

    /// <summary>User-entered query text without the trigger marker.</summary>
    public string Query { get; init; } = string.Empty;

    /// <summary>Maximum number of items to return.</summary>
    public int Limit { get; init; } = 8;

    /// <summary>Sequence assigned by <see cref="DocumentAutocompleteProviderRunner"/>.</summary>
    public long Sequence { get; init; }
}

/// <summary>Runtime request raised when the JS-owned editor detects an autocomplete trigger.</summary>
public sealed class DocumentAutocompleteTriggerRequest
{
    /// <summary>Trigger id detected by JavaScript.</summary>
    public string? TriggerId { get; init; }

    /// <summary>Marker text detected before the query.</summary>
    public string? Marker { get; init; }

    /// <summary>Query text after the marker.</summary>
    public string? Query { get; init; }

    /// <summary>Runtime block id that contains the marker.</summary>
    public string? BlockId { get; init; }

    /// <summary>Marker start offset in the block text.</summary>
    public int StartOffset { get; init; }

    /// <summary>Caret offset after the query.</summary>
    public int EndOffset { get; init; }
}

/// <summary>Autocomplete search result.</summary>
public sealed class DocumentAutocompleteResult
{
    /// <summary>Items returned by the provider.</summary>
    public IReadOnlyList<DocumentAutocompleteItem> Items { get; init; } = [];

    /// <summary>Optional non-fatal warning shown in the menu.</summary>
    public string? WarningMessage { get; init; }

    /// <summary>Request sequence used to discard out-of-order responses.</summary>
    public long Sequence { get; init; }

    /// <summary>True when this response was superseded by a newer request.</summary>
    public bool IsStale { get; init; }

    /// <summary>True when this request was canceled before the provider completed.</summary>
    public bool IsCanceled { get; init; }

    /// <summary>Creates a stale result for a superseded request.</summary>
    public static DocumentAutocompleteResult Stale(long sequence) => new()
    {
        Sequence = sequence,
        IsStale = true
    };

    /// <summary>Creates a canceled result for an aborted request.</summary>
    public static DocumentAutocompleteResult Canceled(long sequence) => new()
    {
        Sequence = sequence,
        IsCanceled = true
    };

    /// <summary>Creates a warning result for a provider failure.</summary>
    public static DocumentAutocompleteResult Warning(long sequence, string message) => new()
    {
        Sequence = sequence,
        WarningMessage = message
    };
}

/// <summary>Provider contract for document autocomplete feeds.</summary>
public interface IDocumentAutocompleteProvider
{
    /// <summary>Searches autocomplete items for the supplied request.</summary>
    Task<DocumentAutocompleteResult> SearchAsync(
        DocumentAutocompleteRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Runs autocomplete searches while canceling older requests and discarding stale responses.</summary>
public sealed class DocumentAutocompleteProviderRunner : IDisposable
{
    private long _latestSequence;
    private CancellationTokenSource? _activeSearch;

    /// <summary>Latest sequence that was allowed to update UI state.</summary>
    public long LatestAppliedSequence { get; private set; }

    /// <summary>Runs a provider search and returns only the latest non-stale response.</summary>
    public async Task<DocumentAutocompleteResult> SearchAsync(
        IDocumentAutocompleteProvider provider,
        DocumentAutocompleteRequest request,
        string providerErrorWarning,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(request);

        var sequence = Interlocked.Increment(ref _latestSequence);
        _activeSearch?.Cancel();
        _activeSearch?.Dispose();
        _activeSearch = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var sequencedRequest = new DocumentAutocompleteRequest
        {
            Trigger = request.Trigger,
            Query = request.Query,
            Limit = request.Limit,
            Sequence = sequence
        };

        try
        {
            var result = await provider.SearchAsync(sequencedRequest, _activeSearch.Token);
            if (sequence != Volatile.Read(ref _latestSequence))
            {
                return DocumentAutocompleteResult.Stale(sequence);
            }

            LatestAppliedSequence = sequence;
            return new DocumentAutocompleteResult
            {
                Items = result.Items.Take(Math.Max(0, sequencedRequest.Limit)).ToList(),
                WarningMessage = result.WarningMessage,
                Sequence = sequence
            };
        }
        catch (OperationCanceledException)
        {
            return DocumentAutocompleteResult.Canceled(sequence);
        }
        catch
        {
            if (sequence != Volatile.Read(ref _latestSequence))
            {
                return DocumentAutocompleteResult.Stale(sequence);
            }

            LatestAppliedSequence = sequence;
            return DocumentAutocompleteResult.Warning(sequence, providerErrorWarning);
        }
    }

    /// <summary>Cancels the active search.</summary>
    public void Cancel()
    {
        _activeSearch?.Cancel();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _activeSearch?.Cancel();
        _activeSearch?.Dispose();
    }
}

/// <summary>Adapts the existing document token provider to the generic autocomplete contract.</summary>
public sealed class TokenDocumentAutocompleteProvider(ITokenDataProvider tokenProvider) : IDocumentAutocompleteProvider
{
    /// <inheritdoc />
    public async Task<DocumentAutocompleteResult> SearchAsync(
        DocumentAutocompleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var tokens = await tokenProvider.SearchTokensAsync(request.Query, cancellationToken);
        var items = tokens
            .Take(Math.Max(0, request.Limit))
            .Select(token => new DocumentAutocompleteItem
            {
                Id = token.Key,
                Label = string.IsNullOrWhiteSpace(token.DisplayName) ? token.Key : token.DisplayName,
                Description = token.Description,
                Kind = DocumentAutocompleteKind.Token,
                Icon = token.Icon,
                Group = token.Category,
                Value = token.Key,
                RendererKey = request.Trigger.RendererKey,
                Metadata =
                {
                    ["key"] = token.Key,
                    ["displayName"] = token.DisplayName,
                    ["description"] = token.Description,
                    ["category"] = token.Category,
                    ["colorClass"] = token.ColorClass,
                    ["typeLabel"] = token.TypeLabel
                }
            })
            .ToList();

        return new DocumentAutocompleteResult
        {
            Items = items,
            Sequence = request.Sequence
        };
    }
}

/// <summary>Adapts the shared people provider to the generic autocomplete contract.</summary>
public sealed class MentionDocumentAutocompleteProvider(ITmPeopleProvider peopleProvider) : IDocumentAutocompleteProvider
{
    /// <inheritdoc />
    public async Task<DocumentAutocompleteResult> SearchAsync(
        DocumentAutocompleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var users = await peopleProvider.SearchAsync(new TmPeopleQuery
        {
            SearchText = request.Query,
            Take = request.Limit
        }, cancellationToken);
        var items = users
            .Take(Math.Max(0, request.Limit))
            .Select(user => new DocumentAutocompleteItem
            {
                Id = user.Id,
                Label = string.IsNullOrWhiteSpace(user.DisplayName) ? UserHandle(user) : user.DisplayName,
                Description = UserHandle(user),
                Kind = DocumentAutocompleteKind.Mention,
                Value = user.Id,
                RendererKey = request.Trigger.RendererKey,
                Metadata =
                {
                    ["id"] = user.Id,
                    ["userName"] = UserHandle(user),
                    ["displayName"] = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Id : user.DisplayName,
                    ["avatarUrl"] = user.AvatarUrl
                }
            })
            .ToList();

        return new DocumentAutocompleteResult
        {
            Items = items,
            Sequence = request.Sequence
        };
    }

    private static string UserHandle(TmUser user)
        => string.IsNullOrWhiteSpace(user.UserName) ? user.Id : user.UserName;
}

/// <summary>Autocomplete provider backed by an in-memory slash command list.</summary>
public sealed class DocumentSlashCommandAutocompleteProvider(
    IEnumerable<DocumentAutocompleteItem> commands) : IDocumentAutocompleteProvider
{
    private readonly IReadOnlyList<DocumentAutocompleteItem> _commands = commands.ToList();

    /// <inheritdoc />
    public Task<DocumentAutocompleteResult> SearchAsync(
        DocumentAutocompleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = request.Query ?? string.Empty;
        var items = _commands
            .Where(command =>
                string.IsNullOrWhiteSpace(query) ||
                command.Label.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (command.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (command.Metadata.TryGetValue("command", out var commandId)
                    && commandId?.Contains(query, StringComparison.OrdinalIgnoreCase) == true))
            .Take(Math.Max(0, request.Limit))
            .ToList();

        return Task.FromResult(new DocumentAutocompleteResult
        {
            Items = items,
            Sequence = request.Sequence
        });
    }
}
