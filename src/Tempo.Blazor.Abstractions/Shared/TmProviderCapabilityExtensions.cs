namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Helpers for working with provider capability flags.</summary>
public static class TmProviderCapabilityExtensions
{
    /// <summary>Returns true when the provider supports all requested capability flags.</summary>
    /// <typeparam name="TCapabilities">Provider-specific capabilities enum.</typeparam>
    /// <param name="provider">Provider advertising capabilities.</param>
    /// <param name="capability">Capability or combined capabilities to check.</param>
    public static bool HasCapability<TCapabilities>(
        this ITmCapabilityProvider<TCapabilities> provider,
        TCapabilities capability)
        where TCapabilities : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(provider);
        return provider.Capabilities.HasCapability(capability);
    }

    /// <summary>Returns true when the capability set includes all requested flags.</summary>
    /// <typeparam name="TCapabilities">Provider-specific capabilities enum.</typeparam>
    /// <param name="capabilities">Capability set to inspect.</param>
    /// <param name="capability">Capability or combined capabilities to check.</param>
    public static bool HasCapability<TCapabilities>(
        this TCapabilities capabilities,
        TCapabilities capability)
        where TCapabilities : struct, Enum
    {
        var current = (Enum)(object)capabilities;
        var requested = (Enum)(object)capability;
        return current.HasFlag(requested);
    }
}
