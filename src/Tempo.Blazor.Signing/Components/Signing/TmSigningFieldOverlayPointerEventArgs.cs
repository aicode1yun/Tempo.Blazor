using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Signing;

/// <summary>Pointer event data for a signing field overlay interaction.</summary>
/// <param name="Field">Signing field associated with the event.</param>
/// <param name="Area">Optional document area associated with the event.</param>
/// <param name="MouseEventArgs">Original Blazor mouse event arguments.</param>
public sealed record TmSigningFieldOverlayPointerEventArgs(
    SigningField Field,
    SigningFieldArea? Area,
    MouseEventArgs MouseEventArgs);
