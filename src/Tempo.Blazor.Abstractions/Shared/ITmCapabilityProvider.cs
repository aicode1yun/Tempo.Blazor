namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>
/// Common contract for providers that advertise optional operations through a
/// provider-specific <c>[Flags]</c> enum.
/// </summary>
/// <typeparam name="TCapabilities">
/// Provider-specific capabilities enum. The enum should define <c>None = 0</c>
/// and mark optional operations with distinct bit flags.
/// </typeparam>
public interface ITmCapabilityProvider<TCapabilities>
    where TCapabilities : struct, Enum
{
    /// <summary>Operations supported by the provider.</summary>
    TCapabilities Capabilities { get; }
}
