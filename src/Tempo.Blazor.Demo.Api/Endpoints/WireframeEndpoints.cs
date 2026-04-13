using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Tempo.Blazor.Demo.Api.Data;

namespace Tempo.Blazor.Demo.Api.Endpoints;

public static class WireframeEndpoints
{
    public static void MapWireframeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/wireframes");

        // GET /api/wireframes  — list all slugs + titles
        group.MapGet("", (MockWireframeStore store) =>
            Results.Ok(store.GetAll().Select(d => new { d.Title, d.Version })));

        // GET /api/wireframes/{slug}  — full document
        group.MapGet("{slug}", (string slug, MockWireframeStore store) =>
        {
            var doc = store.Get(slug);
            return doc is not null ? Results.Ok(doc) : Results.NotFound();
        });

        // PUT /api/wireframes/{slug}  — replace/create a document
        group.MapPut("{slug}", (string slug, WireframeDocument doc, MockWireframeStore store) =>
        {
            store.Upsert(slug, doc);
            return Results.Ok(doc);
        });

        // POST /api/wireframes  — create a new document (slug derived from title)
        group.MapPost("", (WireframeDocument doc, MockWireframeStore store) =>
        {
            var slug = SlugFrom(doc.Title);
            store.Upsert(slug, doc);
            return Results.Created($"/api/wireframes/{slug}", doc);
        });
    }

    private static string SlugFrom(string title) =>
        title.ToLowerInvariant()
             .Replace(' ', '-')
             .Replace(".", "")
             .Trim('-');
}
