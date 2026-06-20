namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Page restriction mode used by a Notion-style page permission provider.</summary>
public enum PageRestrictionMode
{
    /// <summary>The page is open to every user with edit permission.</summary>
    Open = 0,

    /// <summary>The page is editable by default, but listed subjects are read/comment/edit scoped.</summary>
    ReadOnlyForSome = 1,

    /// <summary>The page is not visible by default; listed subjects receive explicit access.</summary>
    EditForSome = 2
}

/// <summary>Subject kind for a page restriction entry.</summary>
public enum PageRestrictionSubjectType
{
    /// <summary>A single user subject.</summary>
    User = 0,

    /// <summary>A group subject.</summary>
    Group = 1
}

/// <summary>Permission level granted by a page restriction entry or effective permission lookup.</summary>
public enum PageRestrictionPermission
{
    /// <summary>No page access.</summary>
    None = 0,

    /// <summary>Can view the page.</summary>
    View = 1,

    /// <summary>Can view and comment on the page.</summary>
    Comment = 2,

    /// <summary>Can view, comment, and edit the page.</summary>
    Edit = 3
}

/// <summary>Single page restriction entry for a user or group subject.</summary>
public sealed class PageRestrictionEntryDto
{
    /// <summary>Subject kind for this entry.</summary>
    public PageRestrictionSubjectType SubjectType { get; set; }

    /// <summary>Stable user or group identifier.</summary>
    public string SubjectId { get; set; } = string.Empty;

    /// <summary>Permission granted to the subject.</summary>
    public PageRestrictionPermission Permission { get; set; } = PageRestrictionPermission.View;
}

/// <summary>Restriction set assigned directly to a page.</summary>
public sealed class PageRestrictionDto
{
    /// <summary>Page identifier owning this restriction set.</summary>
    public Guid PageId { get; set; }

    /// <summary>Restriction mode for this page.</summary>
    public PageRestrictionMode Mode { get; set; } = PageRestrictionMode.Open;

    /// <summary>Explicit user/group entries for this page.</summary>
    public IReadOnlyList<PageRestrictionEntryDto> Entries { get; set; } = [];
}

/// <summary>Effective permission for a page after applying inherited restrictions.</summary>
public sealed class PageEffectivePermissionDto
{
    /// <summary>Page for which the lookup was requested.</summary>
    public Guid PageId { get; set; }

    /// <summary>User identifier used by the permission lookup.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Resolved effective permission.</summary>
    public PageRestrictionPermission Permission { get; set; } = PageRestrictionPermission.Edit;

    /// <summary>True when the winning restriction was inherited from an ancestor page.</summary>
    public bool IsInherited { get; set; }

    /// <summary>Page that supplied the winning restriction, when any restriction was applied.</summary>
    public Guid? SourcePageId { get; set; }

    /// <summary>Restriction mode from the source page or Open when no restriction applies.</summary>
    public PageRestrictionMode Mode { get; set; } = PageRestrictionMode.Open;
}
