using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Signing;

/// <summary>Pointer event data for a document page viewer interaction.</summary>
/// <param name="Page">Document page associated with the event.</param>
/// <param name="MouseEventArgs">Original Blazor mouse event arguments.</param>
public sealed record TmDocumentPageViewerPointerEventArgs(SigningDocumentPage Page, MouseEventArgs MouseEventArgs);
