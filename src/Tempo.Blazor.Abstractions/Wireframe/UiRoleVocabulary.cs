namespace Tempo.Blazor.Components.Wireframe;

/// <summary>
/// Merged, searchable UI role vocabulary backed by registered
/// <see cref="IUiRoleVocabularySource"/> implementations.
/// </summary>
public sealed class UiRoleVocabulary
{
    private readonly IReadOnlyList<UiRoleDefinition> _roles;
    private readonly IReadOnlyDictionary<string, UiRoleDefinition> _rolesBySlug;
    private readonly IReadOnlyDictionary<string, UiRoleDefinition> _rolesBySynonym;

    /// <summary>Builds a deterministic vocabulary from all supplied sources.</summary>
    public UiRoleVocabulary(IEnumerable<IUiRoleVocabularySource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var merged = new Dictionary<string, RoleMergeState>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources
            .OrderBy(source => source.Priority)
            .ThenBy(source => source.SourceId, StringComparer.Ordinal))
        {
            foreach (var role in source.GetRoles())
            {
                if (!merged.TryGetValue(role.Slug, out var state))
                {
                    state = new RoleMergeState(role, source.Priority);
                    merged[role.Slug] = state;
                }

                state.Merge(role, source.Priority);
            }
        }

        _roles = merged.Values
            .Select(state => state.ToDefinition())
            .OrderBy(role => role.Slug, StringComparer.Ordinal)
            .ToArray();

        _rolesBySlug = _roles.ToDictionary(role => role.Slug, StringComparer.OrdinalIgnoreCase);
        _rolesBySynonym = BuildSynonymIndex(_roles);
    }

    /// <summary>Returns all roles ordered by slug.</summary>
    public IReadOnlyList<UiRoleDefinition> GetAll() => _roles;

    /// <summary>Finds a role by slug or synonym using case-insensitive matching.</summary>
    public UiRoleDefinition? Find(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        if (_rolesBySlug.TryGetValue(normalized, out var bySlug))
            return bySlug;

        return _rolesBySynonym.TryGetValue(normalized, out var bySynonym)
            ? bySynonym
            : null;
    }

    private static Dictionary<string, UiRoleDefinition> BuildSynonymIndex(IReadOnlyList<UiRoleDefinition> roles)
    {
        var index = new Dictionary<string, UiRoleDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roles)
        {
            foreach (var synonym in role.Synonyms)
            {
                index.TryAdd(synonym, role);
            }
        }

        return index;
    }

    private sealed class RoleMergeState
    {
        private readonly HashSet<string> _synonyms = new(StringComparer.OrdinalIgnoreCase);
        private UiRoleDefinition _metadata;
        private int _metadataPriority;

        public RoleMergeState(UiRoleDefinition metadata, int metadataPriority)
        {
            _metadata = metadata;
            _metadataPriority = metadataPriority;
        }

        public void Merge(UiRoleDefinition role, int priority)
        {
            if (priority >= _metadataPriority)
            {
                _metadata = role;
                _metadataPriority = priority;
            }

            foreach (var synonym in role.Synonyms)
            {
                _synonyms.Add(synonym);
            }
        }

        public UiRoleDefinition ToDefinition()
            => new(
                _metadata.Slug,
                _metadata.DisplayName,
                _metadata.Definition,
                _synonyms
                    .OrderBy(synonym => synonym, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(synonym => synonym, StringComparer.Ordinal)
                    .ToArray());
    }
}
