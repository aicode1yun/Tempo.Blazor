namespace Tempo.Blazor.Components.Wireframe;

/// <summary>
/// Identifies the application-specific component scope used by wireframe registries.
/// Scoped custom component types are namespaced as <c>app:{appId}:{localType}</c>.
/// </summary>
public sealed class WireframeComponentScope
{
    private const string AppPrefix = "app:";

    private WireframeComponentScope(string appId)
    {
        AppId = NormalizeAppId(appId);
    }

    /// <summary>Application id for this component scope.</summary>
    public string AppId { get; }

    /// <summary>Type prefix used for custom components in this scope.</summary>
    public string TypePrefix => $"{AppPrefix}{AppId}:";

    /// <summary>Creates a component scope for the supplied application id.</summary>
    public static WireframeComponentScope ForApp(string appId) => new(appId);

    /// <summary>Creates a component scope for the supplied application id.</summary>
    public static WireframeComponentScope ForApp(Guid appId)
    {
        if (appId == Guid.Empty)
            throw new ArgumentException("Application id must not be empty.", nameof(appId));

        return new WireframeComponentScope(appId.ToString("D"));
    }

    /// <summary>Creates a scope when an application id is present; otherwise returns null.</summary>
    public static WireframeComponentScope? FromAppId(string? appId)
        => string.IsNullOrWhiteSpace(appId) ? null : new WireframeComponentScope(appId);

    /// <summary>Returns true when <paramref name="type"/> is an app-scoped component type.</summary>
    public static bool IsScopedType(string? type)
        => TryGetAppId(type, out _);

    /// <summary>Attempts to extract the application id from an app-scoped component type.</summary>
    public static bool TryGetAppId(string? type, out string? appId)
    {
        appId = null;
        if (string.IsNullOrWhiteSpace(type)
            || !type.StartsWith(AppPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var firstSeparator = AppPrefix.Length;
        var secondSeparator = type.IndexOf(':', firstSeparator);
        if (secondSeparator <= firstSeparator || secondSeparator == type.Length - 1)
            return false;

        var parsedAppId = type[firstSeparator..secondSeparator];
        if (string.IsNullOrWhiteSpace(parsedAppId))
            return false;

        appId = parsedAppId;
        return true;
    }

    /// <summary>Returns the local component type name without the app scope prefix.</summary>
    public static string GetLocalType(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        if (!TryGetAppId(type, out _))
            return type.Trim();

        var secondSeparator = type.IndexOf(':', AppPrefix.Length);
        return type[(secondSeparator + 1)..].Trim();
    }

    /// <summary>Returns the scoped type for a local component type.</summary>
    public string NamespaceType(string localType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localType);
        if (TryGetAppId(localType, out var scopedAppId))
        {
            if (!MatchesAppId(scopedAppId))
            {
                throw new ArgumentException(
                    $"Component type '{localType}' belongs to a different application scope.",
                    nameof(localType));
            }

            return localType.Trim();
        }

        var trimmed = localType.Trim();
        if (trimmed.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Scoped custom component local type names must not contain ':'.",
                nameof(localType));
        }

        return TypePrefix + trimmed;
    }

    /// <summary>Returns true when <paramref name="appId"/> identifies the same scope.</summary>
    public bool MatchesAppId(string? appId)
        => !string.IsNullOrWhiteSpace(appId)
           && string.Equals(AppId, NormalizeAppId(appId), StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns true when <paramref name="type"/> belongs to this app scope.</summary>
    public bool ContainsType(string type)
        => TryGetAppId(type, out var appId) && MatchesAppId(appId);

    /// <summary>Returns the target pack id for an app-scoped pack.</summary>
    public static string AppPackId(string appId)
        => AppPrefix + NormalizeAppId(appId);

    /// <summary>
    /// Returns true when a schema/definition is visible for the supplied document target packs.
    /// Null or empty target lists preserve legacy visibility; built-ins are always visible.
    /// </summary>
    public static bool IsVisibleInTargetPacks(
        string? scopeAppId,
        bool isBuiltIn,
        IReadOnlyList<string>? targetPackIds)
    {
        if (isBuiltIn || targetPackIds is null || targetPackIds.Count == 0)
            return true;

        if (string.IsNullOrWhiteSpace(scopeAppId))
            return false;

        var normalizedScope = NormalizeAppId(scopeAppId);
        var appPackId = AppPackId(normalizedScope);
        return targetPackIds.Any(target =>
            string.Equals(target?.Trim(), appPackId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(target?.Trim(), normalizedScope, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public override string ToString() => AppId;

    private static string NormalizeAppId(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        var trimmed = appId.Trim();
        if (trimmed.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Application id for a wireframe component scope must not contain ':'.",
                nameof(appId));
        }

        return trimmed;
    }
}
