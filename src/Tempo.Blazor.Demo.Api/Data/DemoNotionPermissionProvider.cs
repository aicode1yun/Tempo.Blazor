using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Data;

public sealed class DemoNotionPermissionProvider : INotionPermissionProvider
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> DemoGroups =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["alice"] = ["editors", "product"],
            ["bob"] = ["readers", "product"],
            ["charlie"] = ["guests"],
            ["dana"] = ["editors"]
        };

    private readonly MockNotionDataStore _pages;
    private readonly object _gate = new();
    private readonly Dictionary<Guid, PageRestrictionDto> _restrictions = new();

    public DemoNotionPermissionProvider(MockNotionDataStore pages)
        => _pages = pages;

    public Task<PageRestrictionDto> GetRestrictionsAsync(Guid pageId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_restrictions.TryGetValue(pageId, out var restrictions)
                ? Clone(restrictions)
                : Open(pageId));
        }
    }

    public Task SetRestrictionsAsync(PageRestrictionDto restrictions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(restrictions);

        var normalized = Clone(restrictions);
        normalized.Entries = normalized.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.SubjectId))
            .GroupBy(entry => (entry.SubjectType, SubjectId: entry.SubjectId.Trim()), StringTupleComparer.Instance)
            .Select(group => new PageRestrictionEntryDto
            {
                SubjectType = group.Key.SubjectType,
                SubjectId = group.Key.SubjectId,
                Permission = group.Last().Permission
            })
            .ToArray();

        lock (_gate)
        {
            if (normalized.Mode == PageRestrictionMode.Open && normalized.Entries.Count == 0)
                _restrictions.Remove(normalized.PageId);
            else
                _restrictions[normalized.PageId] = normalized;
        }

        return Task.CompletedTask;
    }

    public async Task<PageEffectivePermissionDto> GetEffectivePermissionAsync(
        Guid pageId,
        string userId,
        IReadOnlyList<string>? groupIds = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Effective(pageId, userId, PageRestrictionPermission.None, false, null, PageRestrictionMode.EditForSome);

        var effectiveGroups = NormalizeGroups(userId, groupIds);
        var currentPageId = pageId;
        var inherited = false;
        var visited = new HashSet<Guid>();

        while (visited.Add(currentPageId))
        {
            PageRestrictionDto? restrictions;
            lock (_gate)
            {
                restrictions = _restrictions.TryGetValue(currentPageId, out var found) ? Clone(found) : null;
            }

            if (restrictions is not null && restrictions.Mode != PageRestrictionMode.Open)
            {
                return Effective(
                    pageId,
                    userId,
                    ResolvePermission(restrictions, userId, effectiveGroups),
                    inherited,
                    currentPageId,
                    restrictions.Mode);
            }

            INotionPage page;
            try
            {
                page = await _pages.GetPageAsync(currentPageId.ToString("D"));
            }
            catch (KeyNotFoundException)
            {
                break;
            }

            if (page.ParentId is null)
                break;

            currentPageId = page.ParentId.Value;
            inherited = true;
        }

        return Effective(pageId, userId, PageRestrictionPermission.Edit, false, null, PageRestrictionMode.Open);
    }

    public void Reset()
    {
        lock (_gate)
        {
            _restrictions.Clear();
        }
    }

    public void SeedE2ERestrictions()
    {
        lock (_gate)
        {
            _restrictions.Clear();
            _restrictions[MockNotionDataStore.Page1Id] = new PageRestrictionDto
            {
                PageId = MockNotionDataStore.Page1Id,
                Mode = PageRestrictionMode.EditForSome,
                Entries =
                [
                    new PageRestrictionEntryDto
                    {
                        SubjectType = PageRestrictionSubjectType.User,
                        SubjectId = "alice",
                        Permission = PageRestrictionPermission.Edit
                    },
                    new PageRestrictionEntryDto
                    {
                        SubjectType = PageRestrictionSubjectType.User,
                        SubjectId = "demo",
                        Permission = PageRestrictionPermission.Edit
                    },
                    new PageRestrictionEntryDto
                    {
                        SubjectType = PageRestrictionSubjectType.Group,
                        SubjectId = "readers",
                        Permission = PageRestrictionPermission.View
                    }
                ]
            };
        }
    }

    private static IReadOnlyList<string> NormalizeGroups(string userId, IReadOnlyList<string>? groupIds)
    {
        var groups = groupIds is { Count: > 0 }
            ? groupIds
            : DemoGroups.TryGetValue(userId, out var demoGroups)
                ? demoGroups
                : [];

        return groups
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Select(group => group.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static PageRestrictionPermission ResolvePermission(
        PageRestrictionDto restrictions,
        string userId,
        IReadOnlyList<string> groupIds)
    {
        var userEntry = restrictions.Entries.LastOrDefault(entry =>
            entry.SubjectType == PageRestrictionSubjectType.User &&
            string.Equals(entry.SubjectId, userId, StringComparison.OrdinalIgnoreCase));

        if (userEntry is not null)
            return userEntry.Permission;

        var matchingGroupPermissions = restrictions.Entries
            .Where(entry => entry.SubjectType == PageRestrictionSubjectType.Group)
            .Where(entry => groupIds.Any(groupId => string.Equals(groupId, entry.SubjectId, StringComparison.OrdinalIgnoreCase)))
            .Select(entry => entry.Permission)
            .ToArray();

        if (matchingGroupPermissions.Length > 0)
            return matchingGroupPermissions.OrderByDescending(permission => (int)permission).First();

        return restrictions.Mode switch
        {
            PageRestrictionMode.ReadOnlyForSome => PageRestrictionPermission.Edit,
            PageRestrictionMode.EditForSome => PageRestrictionPermission.None,
            _ => PageRestrictionPermission.Edit
        };
    }

    private static PageEffectivePermissionDto Effective(
        Guid pageId,
        string userId,
        PageRestrictionPermission permission,
        bool inherited,
        Guid? sourcePageId,
        PageRestrictionMode mode) =>
        new()
        {
            PageId = pageId,
            UserId = userId,
            Permission = permission,
            IsInherited = inherited,
            SourcePageId = sourcePageId,
            Mode = mode
        };

    private static PageRestrictionDto Open(Guid pageId) => new()
    {
        PageId = pageId,
        Mode = PageRestrictionMode.Open,
        Entries = []
    };

    private static PageRestrictionDto Clone(PageRestrictionDto source) => new()
    {
        PageId = source.PageId,
        Mode = source.Mode,
        Entries = source.Entries
            .Select(entry => new PageRestrictionEntryDto
            {
                SubjectType = entry.SubjectType,
                SubjectId = entry.SubjectId,
                Permission = entry.Permission
            })
            .ToArray()
    };

    private sealed class StringTupleComparer : IEqualityComparer<(PageRestrictionSubjectType SubjectType, string SubjectId)>
    {
        public static readonly StringTupleComparer Instance = new();

        public bool Equals(
            (PageRestrictionSubjectType SubjectType, string SubjectId) x,
            (PageRestrictionSubjectType SubjectType, string SubjectId) y) =>
            x.SubjectType == y.SubjectType &&
            string.Equals(x.SubjectId, y.SubjectId, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((PageRestrictionSubjectType SubjectType, string SubjectId) obj)
            => HashCode.Combine(obj.SubjectType, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.SubjectId));
    }
}
