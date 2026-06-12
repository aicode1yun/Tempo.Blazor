# Tempo.Blazor.Mcp

[Model Context Protocol](https://modelcontextprotocol.io/) tools that let an LLM design and read
Tempo.Blazor **wireframes** programmatically: list the available components, build a design from a
batch of operations, validate it against the component schema, and produce a deterministic
implementation brief.

The package ships the tool implementations and their dependencies only — it is **transport- and
host-agnostic**. The host application owns the MCP server, the storage, and the wiring.

## Installation

```
dotnet add package Tempo.Blazor.Mcp
dotnet add package ModelContextProtocol.AspNetCore   # the host's transport + hosting
```

`Tempo.Blazor.Mcp` references `ModelContextProtocol.Core` (the `[McpServerTool]` attributes) and
`Tempo.Blazor.Abstractions`. The host adds `ModelContextProtocol.AspNetCore` (or any other
transport) to actually serve the tools.

## Hosting

```csharp
// 1. Supply the storage the tools read and write through (host-specific implementations).
builder.Services.AddSingleton<ITempoDocumentLibraryProvider, MyDocumentLibraryProvider>();
builder.Services.AddSingleton<IWireframeDocumentProvider, MyWireframeDocumentProvider>();

// 2. Register the tools' own dependencies (the component schema registry).
builder.Services.AddTempoWireframeMcpTools();

// 3. Map the tools onto an MCP server.
builder.Services.AddMcpServer()
    .WithHttpTransport()                                            // streamable HTTP (stateful)
    .WithToolsFromAssembly(typeof(TempoWireframeMcp).Assembly);

var app = builder.Build();
app.MapMcp("/mcp");
```

> **Use `WithToolsFromAssembly`, not `WithTools(TempoWireframeMcp.ToolTypes)`.** The assembly scan
> advertises the `tools` capability in the initialize handshake; registering by type list does not,
> and clients then see *"Method tools/list not available"*. `ToolTypes` remains public for hosts
> with a custom registration path, but the assembly form is the supported default.

> **Keep the HTTP transport stateful** (do not pass `Stateless = true`). The streamable-HTTP
> handshake requires a session: the client sends `initialize`, the server returns an
> `Mcp-Session-Id` header, the client replies with a `notifications/initialized` notification and
> echoes that header on every subsequent call.

### Surfacing failures as tool results

The tools return their own `{ "success": false, ... }` envelopes for validation, not-found and
conflict cases (see [Result contract](#result-contract)). To convert *unexpected* exceptions —
and the `TempoDocumentConflictException` thrown by a concurrency-aware store — into the same
envelope instead of a JSON-RPC protocol error, add a call-tool filter:

```csharp
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithRequestFilters(filters => filters.AddCallToolFilter(next => async (ctx, ct) =>
    {
        try { return await next(ctx, ct); }
        catch (TempoDocumentConflictException ex)
        {
            return new CallToolResult { IsError = false, Content =
                [new TextContentBlock { Text = McpToolResults.Failure(McpToolResults.Conflict, ex.Message) }] };
        }
        catch (Exception ex)
        {
            return new CallToolResult { IsError = false, Content =
                [new TextContentBlock { Text = McpToolResults.Failure(McpToolResults.Error, ex.Message) }] };
        }
    }))
    .WithToolsFromAssembly(typeof(TempoWireframeMcp).Assembly);
```

## Implementing storage

The tools depend on two abstractions from `Tempo.Blazor.Abstractions`; the host implements both
over its own persistence (database, file store, in-memory, …):

| Abstraction | Responsibility |
|-------------|----------------|
| `ITempoDocumentLibraryProvider` | Browse / list metadata: folder tree, paged listing, single-entry lookup (`GetEntryAsync` returns `ModifiedAt` and the preview), plus optional create-folder / rename / delete advertised via `Capabilities`. |
| `IWireframeDocumentProvider` | Load and save the wireframe **payload** by id. Loading returns the document JSON; saving persists a built/validated design. |

**Optimistic concurrency contract.** `wireframe_get_document` returns `modifiedAt`, an opaque
write token. The write tools (`wireframe_apply_operations`, `wireframe_replace_document`) accept an
optional `expectedModifiedAt`. A concurrency-aware store should compare it against the current
`ModifiedAt` (with ~1 ms tolerance for round-trip precision) and throw
`TempoDocumentConflictException` on mismatch — the filter above turns that into a `conflict`
result so the LLM can re-read and retry. Omitting `expectedModifiedAt` performs a last-writer-wins
save.

**Change notification (optional).** If the host also wires `Tempo.Blazor.Collaboration`, the
*store* (not the MCP tools) publishes an `ITempoDocumentChangePublisher` change after a successful
save. Open editors and embedded NotionEditor blocks subscribed via `ITempoDocumentChangeNotifier`
then refresh live. The tools deliberately do **not** publish, to avoid a double refresh.

## Tools

All tools take and return JSON strings. Successful responses merge their data at the top level
alongside `"success": true`; failures use the [result contract](#result-contract).

| Tool | Purpose |
|------|---------|
| `wireframe_list_components` | List placeable components (`compact=true` first to keep the response small; filter by `category`). |
| `wireframe_get_component_schema` | Full property contract for one type (dimensions + every prop with type/default/allowed values). Suggests a correction when the type is misspelled. |
| `wireframe_list_documents` | List stored wireframes (id, name, folder, last-modified); filter by `folderPath` or `search`. |
| `wireframe_get_document` | Get one document: `modifiedAt` (the concurrency token) + the full document JSON. |
| `wireframe_create_document` | Create a new empty wireframe with a title; returns its id and `modifiedAt`. |
| `wireframe_validate_document` | Validate document JSON against the schema; returns `valid` + precise `validationErrors`. |
| `wireframe_apply_operations` | Apply an ordered batch of edit ops and save. Validated before persistence — nothing is saved if invalid. |
| `wireframe_replace_document` | Replace the whole document with provided JSON and save (also validated first). |
| `wireframe_get_implementation_brief` | Deterministic brief: each page's regions (header/sidebar/content/footer inferred from geometry), components used with counts, and navigation flows from connectors. |

### Operations (`wireframe_apply_operations`)

`operationsJson` is a JSON array; each item carries an `op` discriminator:

`setTitle` · `addPage` · `updatePage` · `removePage` · `setCanvasSize` ·
`addElement` · `updateElement` · `removeElement` ·
`addConnector` · `updateConnector` · `removeConnector`

The whole batch is validated against the component schema **before** anything is saved, so a
partially-applied design is never persisted.

## Tool flow for an LLM

A typical design session:

1. **Discover** — `wireframe_list_components` (compact) to see what is placeable, then
   `wireframe_get_component_schema` for the exact props of the types you'll use.
2. **Create** — `wireframe_create_document` → keep the returned `id`.
3. **Build** — `wireframe_apply_operations` with `setCanvasSize` + `addElement`/`addConnector`
   ops. The response reports how many ops `applied`.
4. **Verify** — `wireframe_get_document` → `wireframe_validate_document` to confirm `valid: true`.
5. **Hand off** — `wireframe_get_implementation_brief` to turn the design into a structured brief
   (regions, components, flows) for building the real page.

For concurrent edits, pass the `modifiedAt` from step 4 as `expectedModifiedAt` on the next write
and handle a `conflict` result by re-reading.

## Result contract

```jsonc
// success — data merged at the top level
{ "success": true, "id": "…", "applied": 6 }

// failure
{ "success": false, "error": "validation_failed", "message": "…",
  "validationErrors": ["Unknown component type 'TmButtonX'. Did you mean 'TmButton'?"] }
```

Error codes: `not_found`, `validation_failed`, `conflict`, `error`. Build these envelopes with the
`McpToolResults` helpers (`Success`, `Failure`).

## Extending to other document kinds

The package is laid out by area (`Wireframe/…`) so diagram and spreadsheet tool sets can be added
under their own namespaces against the same `ITempoDocumentLibraryProvider` and a kind-specific
payload provider. Only wireframe tools ship today.
</content>
</invoke>
