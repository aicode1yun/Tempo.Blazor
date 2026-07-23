using System.Text.Json;
using System.Text.Json.Serialization;
using Tempo.Blazor.NotionEditor.Enums;

namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>A complete, canonical snapshot of one Notion page aggregate.</summary>
public sealed class NotionPageSnapshot
{
    /// <summary>Current schema version emitted by this library.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Schema version of the snapshot.</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Page metadata owned by the aggregate.</summary>
    [JsonPropertyName("page")]
    public NotionPageState Page { get; set; } = new();

    /// <summary>
    /// All blocks in deterministic document order.
    /// </summary>
    /// <remarks>
    /// The list is flat. <see cref="NotionBlockSnapshot.ParentBlockId"/> and
    /// <see cref="NotionBlockSnapshot.Order"/> define the logical tree without duplicating children
    /// or leaking renderer-specific grid nodes.
    /// </remarks>
    [JsonPropertyName("blocks")]
    public IReadOnlyList<NotionBlockSnapshot> Blocks { get; set; } = [];

    /// <summary>Opaque provider-owned optimistic concurrency token.</summary>
    [JsonPropertyName("concurrencyToken")]
    public string ConcurrencyToken { get; set; } = string.Empty;

    /// <summary>Provider-supplied digest of the canonical page content.</summary>
    [JsonPropertyName("digest")]
    public string Digest { get; set; } = string.Empty;
}

/// <summary>Canonical mutable metadata for a page inside an aggregate snapshot.</summary>
public sealed class NotionPageState
{
    /// <summary>Stable page identifier.</summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>Optional parent page identifier.</summary>
    [JsonPropertyName("parentPageId")]
    public Guid? ParentPageId { get; set; }

    /// <summary>Page title.</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional page description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Optional containing space identifier.</summary>
    [JsonPropertyName("spaceId")]
    public string? SpaceId { get; set; }

    /// <summary>Page labels in stable display order.</summary>
    [JsonPropertyName("labels")]
    public IReadOnlyList<string> Labels { get; set; } = [];

    /// <summary>Optional emoji icon.</summary>
    [JsonPropertyName("iconEmoji")]
    public string? IconEmoji { get; set; }

    /// <summary>Optional image icon URL.</summary>
    [JsonPropertyName("iconImageUrl")]
    public string? IconImageUrl { get; set; }

    /// <summary>Optional cover image URL.</summary>
    [JsonPropertyName("coverImageUrl")]
    public string? CoverImageUrl { get; set; }

    /// <summary>Optional normalized vertical cover image position.</summary>
    [JsonPropertyName("coverImagePositionY")]
    public double? CoverImagePositionY { get; set; }

    /// <summary>Whether the page uses the full editor width.</summary>
    [JsonPropertyName("isFullWidth")]
    public bool IsFullWidth { get; set; }

    /// <summary>Whether the page uses compact body text.</summary>
    [JsonPropertyName("isSmallText")]
    public bool IsSmallText { get; set; }

    /// <summary>Whether editing is locked.</summary>
    [JsonPropertyName("isLocked")]
    public bool IsLocked { get; set; }

    /// <summary>Creation timestamp in UTC.</summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>Optional identifier of the creating user.</summary>
    [JsonPropertyName("createdByUserId")]
    public string? CreatedByUserId { get; set; }

    /// <summary>Last edit timestamp in UTC.</summary>
    [JsonPropertyName("lastEditedAt")]
    public DateTime LastEditedAt { get; set; }

    /// <summary>Optional identifier of the last editing user.</summary>
    [JsonPropertyName("lastEditedByUserId")]
    public string? LastEditedByUserId { get; set; }

    /// <summary>Whether the page is in the recycle bin.</summary>
    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }

    /// <summary>Optional deletion timestamp in UTC.</summary>
    [JsonPropertyName("deletedAt")]
    public DateTime? DeletedAt { get; set; }

    /// <summary>Whether the page is a favorite.</summary>
    [JsonPropertyName("isFavorite")]
    public bool IsFavorite { get; set; }
}

/// <summary>A canonical block in a page aggregate snapshot.</summary>
public sealed class NotionBlockSnapshot
{
    /// <summary>Stable block identifier allocated before save.</summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>Stable identifier of the owning page.</summary>
    [JsonPropertyName("pageId")]
    public Guid PageId { get; set; }

    /// <summary>Optional parent block identifier; null denotes a page-level block.</summary>
    [JsonPropertyName("parentBlockId")]
    public Guid? ParentBlockId { get; set; }

    /// <summary>Block type.</summary>
    [JsonPropertyName("type")]
    public BlockType Type { get; set; }

    /// <summary>Zero-based order among blocks with the same parent.</summary>
    [JsonPropertyName("order")]
    public int Order { get; set; }

    /// <summary>
    /// Canonical JSON content for the declared <see cref="Type"/>.
    /// </summary>
    /// <remarks>
    /// Table and table-row content use <see cref="NotionAuthoringTable"/> and
    /// <see cref="NotionAuthoringTableRow"/>. Other block types retain their existing canonical
    /// content shapes until their strict schemas are introduced.
    /// </remarks>
    [JsonPropertyName("content")]
    public JsonElement Content { get; set; } = JsonSerializer.SerializeToElement(
        new Dictionary<string, object?>(),
        NotionAggregateJson.Options);

    /// <summary>Creation timestamp in UTC.</summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>Last edit timestamp in UTC.</summary>
    [JsonPropertyName("lastEditedAt")]
    public DateTime LastEditedAt { get; set; }
}

/// <summary>Result of loading a Notion page aggregate.</summary>
public sealed class NotionAggregateLoadResult
{
    /// <summary>Whether the requested page or block was found.</summary>
    [JsonPropertyName("found")]
    public bool Found { get; set; }

    /// <summary>Loaded owning page snapshot when found.</summary>
    [JsonPropertyName("snapshot")]
    public NotionPageSnapshot? Snapshot { get; set; }

    /// <summary>Matched block identifier when the aggregate was loaded by block id.</summary>
    [JsonPropertyName("matchedBlockId")]
    public Guid? MatchedBlockId { get; set; }

    /// <summary>Structured load issues.</summary>
    [JsonPropertyName("issues")]
    public IReadOnlyList<NotionAggregateIssue> Issues { get; set; } = [];
}

/// <summary>An atomic request to replace one or more complete page aggregates.</summary>
public sealed class NotionAggregateSaveRequest
{
    /// <summary>Page replacements participating in the single atomic transaction.</summary>
    [JsonPropertyName("pages")]
    public IReadOnlyList<NotionPageSave> Pages { get; set; } = [];
}

/// <summary>One page replacement inside an atomic aggregate save.</summary>
public sealed class NotionPageSave
{
    /// <summary>Complete replacement snapshot.</summary>
    [JsonPropertyName("snapshot")]
    public NotionPageSnapshot Snapshot { get; set; } = new();

    /// <summary>Opaque concurrency token from the snapshot on which this replacement is based.</summary>
    [JsonPropertyName("baseConcurrencyToken")]
    public string BaseConcurrencyToken { get; set; } = string.Empty;
}

/// <summary>Result of one atomic aggregate save transaction.</summary>
public sealed class NotionAggregateSaveResult
{
    /// <summary>Whether every requested page was committed.</summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>Always true because partial persistence is outside this contract.</summary>
    [JsonPropertyName("atomic")]
    public bool Atomic => true;

    /// <summary>Whether the transaction failed due to optimistic concurrency.</summary>
    [JsonPropertyName("conflict")]
    public bool Conflict { get; set; }

    /// <summary>Saved page metadata, ordered like the request, after a successful commit.</summary>
    [JsonPropertyName("pages")]
    public IReadOnlyList<NotionSavedPage> Pages { get; set; } = [];

    /// <summary>
    /// Deterministic conflict details, ordered by <see cref="NotionPageConflict.PageId"/>.
    /// </summary>
    [JsonPropertyName("conflicts")]
    public IReadOnlyList<NotionPageConflict> Conflicts { get; set; } = [];

    /// <summary>Structured errors and warnings for the transaction.</summary>
    [JsonPropertyName("issues")]
    public IReadOnlyList<NotionAggregateIssue> Issues { get; set; } = [];
}

/// <summary>Provider metadata returned for one successfully saved page.</summary>
public sealed class NotionSavedPage
{
    /// <summary>Saved page identifier.</summary>
    [JsonPropertyName("pageId")]
    public Guid PageId { get; set; }

    /// <summary>New opaque concurrency token.</summary>
    [JsonPropertyName("concurrencyToken")]
    public string ConcurrencyToken { get; set; } = string.Empty;

    /// <summary>Digest of the saved canonical page.</summary>
    [JsonPropertyName("digest")]
    public string Digest { get; set; } = string.Empty;

    /// <summary>Schema version of the saved snapshot.</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = NotionPageSnapshot.CurrentSchemaVersion;
}

/// <summary>Deterministic optimistic-concurrency conflict for one page.</summary>
public sealed class NotionPageConflict
{
    /// <summary>Conflicting page identifier.</summary>
    [JsonPropertyName("pageId")]
    public Guid PageId { get; set; }

    /// <summary>Opaque token supplied by the caller.</summary>
    [JsonPropertyName("expectedConcurrencyToken")]
    public string ExpectedConcurrencyToken { get; set; } = string.Empty;

    /// <summary>Current opaque provider token, when disclosure is supported.</summary>
    [JsonPropertyName("currentConcurrencyToken")]
    public string? CurrentConcurrencyToken { get; set; }

    /// <summary>Current provider digest, when disclosure is supported.</summary>
    [JsonPropertyName("currentDigest")]
    public string? CurrentDigest { get; set; }
}

/// <summary>A path-aware error or warning produced by aggregate loading, validation, or saving.</summary>
public sealed class NotionAggregateIssue
{
    /// <summary>Stable machine-readable issue code.</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Issue severity.</summary>
    [JsonPropertyName("severity")]
    public NotionIssueSeverity Severity { get; set; }

    /// <summary>Human-readable explanation intended for a developer or agent.</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>JSONPath-like location of the invalid or conflicting value.</summary>
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    /// <summary>Optional actionable remediation.</summary>
    [JsonPropertyName("suggestedFix")]
    public string? SuggestedFix { get; set; }
}

/// <summary>Severity of a structured Notion aggregate issue.</summary>
public enum NotionIssueSeverity
{
    /// <summary>Informational diagnostic.</summary>
    Info,

    /// <summary>Non-blocking compatibility or normalization warning.</summary>
    Warning,

    /// <summary>Blocking error.</summary>
    Error
}
