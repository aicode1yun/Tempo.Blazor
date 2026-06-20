using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Signing;

/// <summary>Pointer event data for starting a signing field resize operation.</summary>
/// <param name="Field">Signing field associated with the resize operation.</param>
/// <param name="Area">Optional document area associated with the resize operation.</param>
/// <param name="Handle">Resize handle that started the operation.</param>
/// <param name="MouseEventArgs">Original Blazor mouse event arguments.</param>
public sealed record TmSigningFieldOverlayResizeEventArgs(
    SigningField Field,
    SigningFieldArea? Area,
    SigningResizeHandle Handle,
    MouseEventArgs MouseEventArgs);
