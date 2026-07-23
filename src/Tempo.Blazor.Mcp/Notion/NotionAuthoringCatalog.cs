using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Mcp.Notion;

/// <summary>
/// Canonical machine-readable vocabulary shared by Notion authoring, validation and MCP
/// documentation.
/// </summary>
internal static class NotionAuthoringCatalog
{
    internal static readonly IReadOnlySet<string> BlockFields =
        new HashSet<string>(["type", "content", "children"], StringComparer.Ordinal);
    internal static readonly IReadOnlySet<string> TableRowFields =
        new HashSet<string>(["cells"], StringComparer.Ordinal);
    internal static readonly IReadOnlySet<string> TableCellFields =
        new HashSet<string>(
        [
            "html",
            "inlines",
            "backgroundColor",
            "textColor",
            "horizontalAlignment",
            "verticalAlignment",
            "rowSpan",
            "columnSpan",
            "width",
            "borders"
        ], StringComparer.Ordinal);
    internal static readonly IReadOnlySet<string> InlineFields =
        new HashSet<string>(
        [
            "text",
            "href",
            "bold",
            "italic",
            "underline",
            "strikethrough",
            "code",
            "textColor",
            "backgroundColor"
        ], StringComparer.Ordinal);
    internal static readonly IReadOnlySet<string> BorderContainerFields =
        new HashSet<string>(["top", "right", "bottom", "left"], StringComparer.Ordinal);
    internal static readonly IReadOnlySet<string> BorderFields =
        new HashSet<string>(["style", "color", "width"], StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<BlockType, Type> ContentTypes =
        BuildContentTypes();
    private static readonly IReadOnlyList<OperationDefinition> OperationDefinitions =
        BuildOperationDefinitions();
    private static readonly IReadOnlyDictionary<string, OperationDefinition> OperationsByName =
        OperationDefinitions.ToDictionary(definition => definition.Name, StringComparer.Ordinal);

    internal static IReadOnlyList<string> SupportedOperationNames { get; } =
        OperationDefinitions.Select(definition => definition.Name).ToList();

    internal static IReadOnlySet<string>? GetOperationFields(string operation)
        => OperationsByName.TryGetValue(operation, out var definition)
            ? definition.AllowedFields
            : null;

    internal static IReadOnlyList<JsonObject> ListBlockTypes()
        => Enum.GetValues<BlockType>()
            .Select(type => GetBlockSchema(
                type,
                includeFields: false,
                includeExample: false))
            .ToList();

    internal static JsonObject? TryGetBlockSchema(BlockType type)
        => ContentTypes.ContainsKey(type)
            ? GetBlockSchema(type, includeFields: true, includeExample: true)
            : null;

    internal static IReadOnlyList<JsonObject> GetOperationCatalog(string? operation)
    {
        var selected = string.IsNullOrWhiteSpace(operation)
            ? OperationDefinitions
            : OperationDefinitions
                .Where(definition =>
                    definition.Name.Equals(operation, StringComparison.OrdinalIgnoreCase))
                .ToList();
        return selected.Select(ToJson).ToList();
    }

    internal static JsonObject GetAuthoringGuide(string? topic)
    {
        var normalizedTopic = string.IsNullOrWhiteSpace(topic)
            ? "all"
            : topic.Trim().ToLowerInvariant();
        var guide = new JsonObject
        {
            ["contractVersion"] = 1,
            ["topic"] = normalizedTopic,
            ["readBeforeWrite"] = new JsonObject
            {
                ["tool"] = "notion_get_block_tree",
                ["instruction"] = "Read the recursive tree and retain pageId, concurrencyToken and digest before authoring."
            },
            ["atomicWrite"] = new JsonObject
            {
                ["tool"] = "notion_apply_block_operations",
                ["operationsArgument"] = "operationsJson",
                ["versionsArgument"] = "expectedPageVersionsJson",
                ["idempotencyInstruction"] = "Generate one stable idempotencyKey per logical request and reuse the same key only with byte-equivalent canonical operations.",
                ["retryInstruction"] = "On conflict, re-read the page, rebuild the request against the new token, and retry with a new idempotencyKey. On an ambiguous transport failure, retry the identical request with the same key."
            },
            ["recursiveChildren"] = new JsonObject
            {
                ["instruction"] = "Use children on createBlock/createBlocks items. Child order is the zero-based array order and every created child id is returned through clientRef mappings.",
                ["example"] = OperationDefinitions.Single(definition => definition.Name == "createBlock")
                    .Example.DeepClone()
            },
            ["createTable"] = new JsonObject
            {
                ["instruction"] = "Use createTable rows with logical origin cells only. rowSpan and columnSpan define covered physical slots; never emit merge markers.",
                ["limits"] = LimitsJson(),
                ["example"] = OperationDefinitions.Single(definition => definition.Name == "createTable")
                    .Example.DeepClone()
            },
            ["patch"] = new JsonObject
            {
                ["instruction"] = "patchBlockContent applies an RFC 7396-style object merge to content while preserving id, page, parent and order.",
                ["example"] = OperationDefinitions.Single(definition => definition.Name == "patchBlockContent")
                    .Example.DeepClone()
            },
            ["move"] = new JsonObject
            {
                ["instruction"] = "moveBlock moves the complete subtree. Supply targetPageId, targetParentBlockId and targetOrder explicitly.",
                ["example"] = OperationDefinitions.Single(definition => definition.Name == "moveBlock")
                    .Example.DeepClone()
            },
            ["discovery"] = new JsonObject
            {
                ["blockSchemaTool"] = "notion_get_block_schema",
                ["operationCatalogTool"] = "notion_get_operation_catalog",
                ["note"] = "Nested schemas are returned as JSON data so MCP tool arguments remain primitive strings, booleans, integers and timestamps."
            }
        };

        if (normalizedTopic == "all")
        {
            return guide;
        }

        var selected = guide[normalizedTopic]?.DeepClone();
        return selected is null
            ? new JsonObject
            {
                ["contractVersion"] = 1,
                ["topic"] = normalizedTopic,
                ["availableTopics"] = new JsonArray(
                    guide.Select(property => property.Key)
                        .Where(key => key is not ("contractVersion" or "topic"))
                        .Select(key => (JsonNode?)JsonValue.Create(key))
                        .ToArray())
            }
            : new JsonObject
            {
                ["contractVersion"] = 1,
                ["topic"] = normalizedTopic,
                ["guide"] = selected
            };
    }

    private static JsonObject GetBlockSchema(
        BlockType type,
        bool includeFields,
        bool includeExample)
    {
        var contentType = ContentTypes[type];
        var schema = new JsonObject
        {
            ["type"] = JsonNamingPolicy.CamelCase.ConvertName(type.ToString()),
            ["description"] = DescribeBlock(type),
            ["contentType"] = contentType.Name,
            ["allowsChildren"] = type is not BlockType.TableRow,
            ["fields"] = includeFields
                ? BuildFields(contentType, contentType, depth: 0)
                : new JsonArray(),
            ["limits"] = type is BlockType.Table or BlockType.TableRow
                ? LimitsJson()
                : new JsonObject()
        };
        if (includeExample)
        {
            schema["example"] = BuildBlockExample(type);
        }

        return schema;
    }

    private static JsonArray BuildFields(Type type, Type rootType, int depth)
    {
        if (depth > 6)
        {
            return [];
        }

        var instance = TryCreate(type);
        var fields = new JsonArray();
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(property => property.GetMethod is not null)
                     .OrderBy(property => property.MetadataToken))
        {
            var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            var propertyType = property.PropertyType;
            var underlying = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            var elementType = TryGetElementType(underlying);
            var nestedType = elementType ?? underlying;
            var defaultValue = instance is null ? null : property.GetValue(instance);
            var nullable = IsNullable(property);
            var required = IsRequired(rootType, property);
            var field = new JsonObject
            {
                ["name"] = name,
                ["jsonType"] = JsonType(propertyType),
                ["required"] = required,
                ["optional"] = !required,
                ["nullable"] = nullable,
                ["default"] = ToNode(defaultValue, propertyType),
                ["description"] = DescribeField(name),
                ["enumValues"] = underlying.IsEnum
                    ? new JsonArray(Enum.GetNames(underlying)
                        .Select(value => JsonValue.Create(
                            JsonNamingPolicy.CamelCase.ConvertName(value)))
                        .ToArray())
                    : new JsonArray(),
                ["minimum"] = Minimum(rootType, name),
                ["maximum"] = Maximum(rootType, name),
                ["maxLength"] = MaxLength(name),
                ["maxItems"] = MaxItems(name),
                ["fields"] = IsNestedObject(nestedType)
                    ? BuildFields(nestedType, nestedType, depth + 1)
                    : new JsonArray(),
                ["example"] = ExampleValue(rootType, name, propertyType, defaultValue)
            };

            if (elementType is not null)
            {
                field["items"] = new JsonObject
                {
                    ["jsonType"] = JsonType(elementType),
                    ["nullable"] = !elementType.IsValueType,
                    ["enumValues"] = elementType.IsEnum
                        ? new JsonArray(Enum.GetNames(elementType)
                            .Select(value => JsonValue.Create(
                                JsonNamingPolicy.CamelCase.ConvertName(value)))
                            .ToArray())
                        : new JsonArray(),
                    ["fields"] = IsNestedObject(elementType)
                        ? BuildFields(elementType, elementType, depth + 1)
                        : new JsonArray()
                };
            }
            else
            {
                field["items"] = null;
            }

            fields.Add(field);
        }

        return fields;
    }

    private static IReadOnlyDictionary<BlockType, Type> BuildContentTypes()
        => new Dictionary<BlockType, Type>
        {
            [BlockType.Paragraph] = typeof(TextBlockContent),
            [BlockType.Heading1] = typeof(HeadingBlockContent),
            [BlockType.Heading2] = typeof(HeadingBlockContent),
            [BlockType.Heading3] = typeof(HeadingBlockContent),
            [BlockType.Quote] = typeof(TextBlockContent),
            [BlockType.Callout] = typeof(CalloutBlockContent),
            [BlockType.Code] = typeof(CodeBlockContent),
            [BlockType.Divider] = typeof(DividerBlockContent),
            [BlockType.Equation] = typeof(EquationBlockContent),
            [BlockType.BulletList] = typeof(ListBlockContent),
            [BlockType.NumberedList] = typeof(ListBlockContent),
            [BlockType.TodoItem] = typeof(TodoBlockContent),
            [BlockType.Toggle] = typeof(ToggleBlockContent),
            [BlockType.Table] = typeof(NotionAuthoringTable),
            [BlockType.TableRow] = typeof(NotionAuthoringTableRow),
            [BlockType.Image] = typeof(ImageBlockContent),
            [BlockType.Video] = typeof(VideoBlockContent),
            [BlockType.Audio] = typeof(AudioBlockContent),
            [BlockType.File] = typeof(FileBlockContent),
            [BlockType.Pdf] = typeof(PdfBlockContent),
            [BlockType.Bookmark] = typeof(BookmarkBlockContent),
            [BlockType.Embed] = typeof(EmbedBlockContent),
            [BlockType.ChildPage] = typeof(ChildPageBlockContent),
            [BlockType.LinkedPage] = typeof(LinkedPageBlockContent),
            [BlockType.Breadcrumb] = typeof(BreadcrumbBlockContent),
            [BlockType.SyncedBlockOrigin] = typeof(SyncedBlockOriginContent),
            [BlockType.SyncedBlockRef] = typeof(SyncedBlockRefContent),
            [BlockType.InlineDatabase] = typeof(InlineDatabaseBlockContent),
            [BlockType.LinkedDatabase] = typeof(LinkedDatabaseBlockContent),
            [BlockType.ColumnList] = typeof(ColumnListBlockContent),
            [BlockType.Column] = typeof(ColumnBlockContent),
            [BlockType.TemplateButton] = typeof(TemplateButtonBlockContent),
            [BlockType.TableOfContents] = typeof(TableOfContentsBlockContent),
            [BlockType.Diagram] = typeof(DiagramBlockContent),
            [BlockType.Wireframe] = typeof(WireframeBlockContent),
            [BlockType.Spreadsheet] = typeof(SpreadsheetBlockContent),
            [BlockType.WorkItem] = typeof(WorkItemBlockContent),
            [BlockType.ContentByLabel] = typeof(ContentByLabelBlockContent),
            [BlockType.IncludePage] = typeof(IncludePageBlockContent),
            [BlockType.ChildrenDisplay] = typeof(ChildrenDisplayBlockContent),
            [BlockType.Excerpt] = typeof(ExcerptBlockContent),
            [BlockType.ExcerptInclude] = typeof(ExcerptIncludeBlockContent),
            [BlockType.PageProperties] = typeof(PagePropertiesBlockContent),
            [BlockType.PagePropertiesReport] = typeof(PagePropertiesReportBlockContent)
        };

    private static IReadOnlyList<OperationDefinition> BuildOperationDefinitions()
    {
        return
        [
            Operation(
                "createBlock",
                ["op", "clientRef", "pageId", "parentBlockId", "order", "block"],
                ["op", "pageId", "block"],
                new JsonObject
                {
                    ["op"] = "createBlock",
                    ["clientRef"] = "intro",
                    ["pageId"] = "11111111-1111-1111-1111-111111111111",
                    ["block"] = new JsonObject
                    {
                        ["type"] = "paragraph",
                        ["content"] = new JsonObject { ["html"] = "Introduction" },
                        ["children"] = new JsonArray(
                            new JsonObject
                            {
                                ["type"] = "paragraph",
                                ["content"] = new JsonObject { ["html"] = "Nested detail" }
                            })
                    }
                }),
            Operation(
                "createBlocks",
                ["op", "clientRef", "pageId", "parentBlockId", "order", "blocks"],
                ["op", "pageId", "blocks"],
                new JsonObject
                {
                    ["op"] = "createBlocks",
                    ["pageId"] = "11111111-1111-1111-1111-111111111111",
                    ["blocks"] = new JsonArray(
                        new JsonObject
                        {
                            ["type"] = "paragraph",
                            ["content"] = new JsonObject { ["html"] = "First" }
                        })
                }),
            Operation(
                "createTable",
                [
                    "op", "clientRef", "pageId", "parentBlockId", "order", "columnCount",
                    "hasHeaderRow", "hasHeaderColumn", "columnAlignments", "columnWidths", "rows"
                ],
                ["op", "pageId", "columnCount", "rows"],
                CreateTableExample()),
            Operation(
                "patchBlockContent",
                ["op", "clientRef", "blockId", "patch"],
                ["op", "blockId", "patch"],
                new JsonObject
                {
                    ["op"] = "patchBlockContent",
                    ["blockId"] = "22222222-2222-2222-2222-222222222222",
                    ["patch"] = new JsonObject { ["html"] = "Updated" }
                }),
            Operation(
                "moveBlock",
                [
                    "op", "clientRef", "blockId", "targetPageId", "targetParentBlockId",
                    "targetOrder"
                ],
                ["op", "blockId", "targetPageId", "targetOrder"],
                new JsonObject
                {
                    ["op"] = "moveBlock",
                    ["blockId"] = "22222222-2222-2222-2222-222222222222",
                    ["targetPageId"] = "11111111-1111-1111-1111-111111111111",
                    ["targetParentBlockId"] = null,
                    ["targetOrder"] = 0
                }),
            Operation(
                "reorderBlocks",
                ["op", "clientRef", "pageId", "parentBlockId", "orderedBlockIds"],
                ["op", "pageId", "orderedBlockIds"],
                new JsonObject
                {
                    ["op"] = "reorderBlocks",
                    ["pageId"] = "11111111-1111-1111-1111-111111111111",
                    ["orderedBlockIds"] = new JsonArray(
                        "22222222-2222-2222-2222-222222222222")
                }),
            Operation(
                "convertBlockType",
                ["op", "clientRef", "blockId", "newType", "content"],
                ["op", "blockId", "newType", "content"],
                new JsonObject
                {
                    ["op"] = "convertBlockType",
                    ["blockId"] = "22222222-2222-2222-2222-222222222222",
                    ["newType"] = "quote",
                    ["content"] = new JsonObject { ["html"] = "Quoted" }
                }),
            Operation(
                "deleteBlock",
                ["op", "clientRef", "blockId"],
                ["op", "blockId"],
                new JsonObject
                {
                    ["op"] = "deleteBlock",
                    ["blockId"] = "22222222-2222-2222-2222-222222222222"
                }),
            Operation(
                "replaceBlocks",
                ["op", "clientRef", "pageId", "parentBlockId", "blocks"],
                ["op", "pageId", "blocks"],
                new JsonObject
                {
                    ["op"] = "replaceBlocks",
                    ["pageId"] = "11111111-1111-1111-1111-111111111111",
                    ["parentBlockId"] = null,
                    ["blocks"] = new JsonArray()
                })
        ];
    }

    private static OperationDefinition Operation(
        string name,
        IReadOnlyList<string> fields,
        IReadOnlyList<string> required,
        JsonObject example)
    {
        var requiredSet = required.ToHashSet(StringComparer.Ordinal);
        return new OperationDefinition(
            name,
            fields.ToHashSet(StringComparer.Ordinal),
            fields.Select(field => OperationField(name, field, requiredSet.Contains(field))).ToList(),
            example);
    }

    private static JsonObject OperationField(string operation, string name, bool required)
    {
        var (jsonType, nullable, defaultValue) = name switch
        {
            "op" or "clientRef" or "pageId" or "blockId" or "targetPageId" or "newType"
                => ("string", false, null),
            "parentBlockId" or "targetParentBlockId"
                => ("string", true, null),
            "order" or "targetOrder" or "columnCount"
                => ("integer", false, null),
            "hasHeaderRow" or "hasHeaderColumn"
                => ("boolean", false, (JsonNode?)JsonValue.Create(false)),
            "block" or "patch" or "content"
                => ("object", false, null),
            _ => ("array", false, (JsonNode?)new JsonArray())
        };
        var field = new JsonObject
        {
            ["name"] = name,
            ["jsonType"] = jsonType,
            ["required"] = required,
            ["optional"] = !required,
            ["nullable"] = nullable,
            ["default"] = defaultValue?.DeepClone(),
            ["description"] = DescribeOperationField(operation, name),
            ["enumValues"] = name == "op"
                ? new JsonArray(JsonValue.Create(operation))
                : name == "newType"
                    ? new JsonArray(Enum.GetNames<BlockType>()
                        .Select(value => JsonValue.Create(
                            JsonNamingPolicy.CamelCase.ConvertName(value)))
                        .ToArray())
                    : new JsonArray(),
            ["fields"] = new JsonArray(),
            ["items"] = null,
            ["example"] = null
        };

        if (name is "block")
        {
            field["fields"] = BlockEnvelopeFields(includeRecursiveChildDetails: true);
        }
        else if (name is "blocks")
        {
            field["items"] = new JsonObject
            {
                ["jsonType"] = "object",
                ["nullable"] = false,
                ["enumValues"] = new JsonArray(),
                ["fields"] = BlockEnvelopeFields(includeRecursiveChildDetails: true)
            };
        }
        else if (name is "rows")
        {
            field["items"] = new JsonObject
            {
                ["jsonType"] = "object",
                ["nullable"] = false,
                ["enumValues"] = new JsonArray(),
                ["fields"] = BuildFields(
                    typeof(NotionAuthoringTableRow),
                    typeof(NotionAuthoringTableRow),
                    depth: 0)
            };
        }
        else if (name is "columnAlignments")
        {
            field["items"] = new JsonObject
            {
                ["jsonType"] = "string",
                ["nullable"] = false,
                ["enumValues"] = new JsonArray(
                    Enum.GetNames<NotionTableHorizontalAlignment>()
                        .Select(value => JsonValue.Create(
                            JsonNamingPolicy.CamelCase.ConvertName(value)))
                        .ToArray()),
                ["fields"] = new JsonArray()
            };
        }
        else if (name is "columnWidths")
        {
            field["items"] = new JsonObject
            {
                ["jsonType"] = "number",
                ["nullable"] = true,
                ["enumValues"] = new JsonArray(),
                ["minimum"] = 1,
                ["fields"] = new JsonArray()
            };
        }
        else if (name is "orderedBlockIds")
        {
            field["items"] = new JsonObject
            {
                ["jsonType"] = "string",
                ["nullable"] = false,
                ["enumValues"] = new JsonArray(),
                ["fields"] = new JsonArray()
            };
        }

        return field;
    }

    private static JsonArray BlockEnvelopeFields(bool includeRecursiveChildDetails)
    {
        var childrenItems = new JsonObject
        {
            ["jsonType"] = "object",
            ["nullable"] = false,
            ["enumValues"] = new JsonArray(),
            ["fields"] = includeRecursiveChildDetails
                ? BlockEnvelopeFields(includeRecursiveChildDetails: false)
                : new JsonArray()
        };
        return new JsonArray(
            new JsonObject
            {
                ["name"] = "type",
                ["jsonType"] = "string",
                ["required"] = true,
                ["optional"] = false,
                ["nullable"] = false,
                ["default"] = null,
                ["description"] = "Canonical BlockType discriminator.",
                ["enumValues"] = new JsonArray(
                    Enum.GetNames<BlockType>()
                        .Select(value => JsonValue.Create(
                            JsonNamingPolicy.CamelCase.ConvertName(value)))
                        .ToArray()),
                ["fields"] = new JsonArray(),
                ["items"] = null,
                ["example"] = "paragraph"
            },
            new JsonObject
            {
                ["name"] = "content",
                ["jsonType"] = "object",
                ["required"] = true,
                ["optional"] = false,
                ["nullable"] = false,
                ["default"] = null,
                ["description"] = "Canonical content object described by notion_get_block_schema for the selected type.",
                ["enumValues"] = new JsonArray(),
                ["fields"] = new JsonArray(),
                ["items"] = null,
                ["example"] = new JsonObject { ["html"] = "Text" }
            },
            new JsonObject
            {
                ["name"] = "children",
                ["jsonType"] = "array",
                ["required"] = false,
                ["optional"] = true,
                ["nullable"] = false,
                ["default"] = new JsonArray(),
                ["description"] = "Ordered recursive strict child block objects.",
                ["enumValues"] = new JsonArray(),
                ["fields"] = new JsonArray(),
                ["items"] = childrenItems,
                ["example"] = new JsonArray()
            });
    }

    private static JsonObject ToJson(OperationDefinition definition)
        => new()
        {
            ["operation"] = definition.Name,
            ["description"] = DescribeOperation(definition.Name),
            ["fields"] = new JsonArray(
                definition.Fields.Select(field => field.DeepClone()).ToArray()),
            ["example"] = definition.Example.DeepClone()
        };

    private static JsonObject BuildBlockExample(BlockType type)
    {
        if (type == BlockType.Table)
        {
            var example = CreateTableExample();
            return new JsonObject
            {
                ["type"] = "table",
                ["content"] = new JsonObject
                {
                    ["columnCount"] = example["columnCount"]?.DeepClone(),
                    ["hasHeaderRow"] = example["hasHeaderRow"]?.DeepClone(),
                    ["hasHeaderColumn"] = example["hasHeaderColumn"]?.DeepClone(),
                    ["columnAlignments"] = example["columnAlignments"]?.DeepClone(),
                    ["columnWidths"] = example["columnWidths"]?.DeepClone()
                },
                ["rows"] = example["rows"]?.DeepClone()
            };
        }
        if (type == BlockType.TableRow)
        {
            return new JsonObject
            {
                ["type"] = "tableRow",
                ["content"] = new JsonObject
                {
                    ["cells"] = new JsonArray(
                        new JsonObject
                        {
                            ["html"] = "<strong>Header</strong>",
                            ["rowSpan"] = 1,
                            ["columnSpan"] = 1
                        })
                }
            };
        }

        return new JsonObject
        {
            ["type"] = JsonNamingPolicy.CamelCase.ConvertName(type.ToString()),
            ["content"] = JsonSerializer.SerializeToNode(
                TryCreate(ContentTypes[type]),
                ContentTypes[type],
                NotionAggregateJson.Options)
        };
    }

    private static JsonObject CreateTableExample()
        => new()
        {
            ["op"] = "createTable",
            ["clientRef"] = "risk-table",
            ["pageId"] = "11111111-1111-1111-1111-111111111111",
            ["columnCount"] = 2,
            ["hasHeaderRow"] = true,
            ["hasHeaderColumn"] = false,
            ["columnAlignments"] = new JsonArray("left", "right"),
            ["columnWidths"] = new JsonArray(220, 120),
            ["rows"] = new JsonArray(
                new JsonObject
                {
                    ["cells"] = new JsonArray(
                        new JsonObject
                        {
                            ["html"] = "<strong>Risk</strong>",
                            ["backgroundColor"] = "#fef3c7",
                            ["textColor"] = "#111827",
                            ["horizontalAlignment"] = "left",
                            ["verticalAlignment"] = "middle",
                            ["rowSpan"] = 1,
                            ["columnSpan"] = 1,
                            ["width"] = 220,
                            ["borders"] = new JsonObject
                            {
                                ["bottom"] = new JsonObject
                                {
                                    ["style"] = "solid",
                                    ["color"] = "#d97706",
                                    ["width"] = 1
                                }
                            }
                        },
                        new JsonObject
                        {
                            ["inlines"] = new JsonArray(
                                new JsonObject
                                {
                                    ["text"] = "Impact",
                                    ["bold"] = true,
                                    ["textColor"] = "#111827"
                                }),
                            ["rowSpan"] = 1,
                            ["columnSpan"] = 1
                        })
                })
        };

    private static JsonObject LimitsJson()
        => new()
        {
            ["maxRows"] = NotionAuthoringLimits.MaxTableRows,
            ["maxColumns"] = NotionAuthoringLimits.MaxTableColumns,
            ["maxPhysicalSlots"] = NotionAuthoringLimits.MaxTableSlots,
            ["maxCellInlines"] = NotionAuthoringLimits.MaxCellInlines,
            ["maxInlineTextLength"] = NotionAuthoringLimits.MaxInlineTextLength,
            ["maxCellHtmlLength"] = NotionAuthoringLimits.MaxCellHtmlLength,
            ["maxTableContentLength"] = NotionAuthoringLimits.MaxTableContentLength,
            ["maxCssColorLength"] = NotionAuthoringLimits.MaxCssColorLength
        };

    private static bool IsRequired(Type rootType, PropertyInfo property)
        => rootType == typeof(NotionAuthoringTable) && property.Name == nameof(NotionAuthoringTable.ColumnCount)
           || rootType == typeof(NotionAuthoringTableRow) && property.Name == nameof(NotionAuthoringTableRow.Cells);

    private static bool IsNullable(PropertyInfo property)
    {
        if (Nullable.GetUnderlyingType(property.PropertyType) is not null)
        {
            return true;
        }
        if (property.PropertyType.IsValueType)
        {
            return false;
        }

        return new NullabilityInfoContext().Create(property).ReadState ==
            NullabilityState.Nullable;
    }

    private static string JsonType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(string) || type == typeof(Guid) || type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) || type.IsEnum)
        {
            return "string";
        }
        if (type == typeof(bool))
        {
            return "boolean";
        }
        if (type == typeof(byte) || type == typeof(short) || type == typeof(int) ||
            type == typeof(long) || type == typeof(ushort) || type == typeof(uint) ||
            type == typeof(ulong))
        {
            return "integer";
        }
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
        {
            return "number";
        }
        if (TryGetElementType(type) is not null)
        {
            return "array";
        }

        return "object";
    }

    private static Type? TryGetElementType(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }
        if (type == typeof(string))
        {
            return null;
        }

        return type.GetInterfaces()
            .Append(type)
            .FirstOrDefault(candidate =>
                candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() is var definition &&
                definition is not null &&
                (definition == typeof(IEnumerable<>) ||
                 definition == typeof(IReadOnlyList<>) ||
                 definition == typeof(IList<>) ||
                 definition == typeof(List<>)))
            ?.GetGenericArguments()[0];
    }

    private static bool IsNestedObject(Type type)
        => JsonType(type) == "object" &&
           type.Namespace?.StartsWith("Tempo.Blazor", StringComparison.Ordinal) == true;

    private static object? TryCreate(Type type)
    {
        try
        {
            return Activator.CreateInstance(type);
        }
        catch (Exception ex) when (ex is MissingMethodException or TargetInvocationException)
        {
            return null;
        }
    }

    private static JsonNode? ToNode(object? value, Type declaredType)
    {
        if (value is null)
        {
            return null;
        }

        return JsonSerializer.SerializeToNode(value, declaredType, NotionAggregateJson.Options);
    }

    private static JsonNode? ExampleValue(
        Type rootType,
        string name,
        Type declaredType,
        object? defaultValue)
    {
        object? example = name switch
        {
            "html" => "<strong>Formatted text</strong>",
            "text" => "Cell text",
            "href" => "https://example.com",
            "backgroundColor" => "#fef3c7",
            "textColor" or "color" => "#111827",
            "columnCount" => 2,
            "rowSpan" or "columnSpan" => 1,
            "width" when rootType == typeof(NotionTableBorder) => 1d,
            "width" when rootType == typeof(NotionAuthoringTableCell) => 220d,
            _ => defaultValue
        };
        return ToNode(example, declaredType);
    }

    private static JsonNode? Minimum(Type rootType, string name)
        => name switch
        {
            "columnCount" or "rowSpan" or "columnSpan" or "width" => JsonValue.Create(1),
            _ => null
        };

    private static JsonNode? Maximum(Type rootType, string name)
        => name switch
        {
            "columnCount" or "columnSpan" => JsonValue.Create(NotionAuthoringLimits.MaxTableColumns),
            "rowSpan" => JsonValue.Create(NotionAuthoringLimits.MaxTableRows),
            _ => null
        };

    private static JsonNode? MaxLength(string name)
        => name switch
        {
            "html" => JsonValue.Create(NotionAuthoringLimits.MaxCellHtmlLength),
            "text" => JsonValue.Create(NotionAuthoringLimits.MaxInlineTextLength),
            "backgroundColor" or "textColor" or "color"
                => JsonValue.Create(NotionAuthoringLimits.MaxCssColorLength),
            _ => null
        };

    private static JsonNode? MaxItems(string name)
        => name switch
        {
            "rows" => JsonValue.Create(NotionAuthoringLimits.MaxTableRows),
            "cells" => JsonValue.Create(NotionAuthoringLimits.MaxTableColumns),
            "inlines" => JsonValue.Create(NotionAuthoringLimits.MaxCellInlines),
            _ => null
        };

    private static string DescribeBlock(BlockType type)
        => type switch
        {
            BlockType.Table => "Logical rich table. Rows are separate tableRow blocks in storage and are embedded as rows by readback.",
            BlockType.TableRow => "Logical table row containing origin cells with optional row and column spans.",
            _ => $"{type} Notion block."
        };

    private static string DescribeField(string name)
        => name switch
        {
            "html" => "Sanitized inline HTML. Plain text or the documented inline formatting tags only.",
            "inlines" => "Structured rich-text runs. When non-empty, structured inlines are authoritative.",
            "backgroundColor" or "textColor" or "color" => "Literal safe CSS color: hex, approved named color, rgb/rgba or hsl/hsla.",
            "rowSpan" => "Number of physical rows covered by this logical origin cell.",
            "columnSpan" => "Number of physical columns covered by this logical origin cell.",
            "cells" => "Logical origin cells in left-to-right order; never include covered merge markers.",
            "columnCount" => "Exact physical column count.",
            _ => $"Canonical {name} value."
        };

    private static string DescribeOperation(string operation)
        => operation switch
        {
            "createBlock" => "Create one block and its recursive children.",
            "createBlocks" => "Create an ordered block forest.",
            "createTable" => "Create one complete logical rich table and its row blocks.",
            "patchBlockContent" => "Merge a strict object patch into block content.",
            "moveBlock" => "Move one complete block subtree, optionally across pages.",
            "reorderBlocks" => "Set the exact sibling order for one parent.",
            "convertBlockType" => "Change a block type and replace its content.",
            "deleteBlock" => "Delete one complete block subtree.",
            "replaceBlocks" => "Replace all children of one page or parent with a new block forest.",
            _ => operation
        };

    private static string DescribeOperationField(string operation, string field)
        => field == "op"
            ? $"Exact discriminator '{operation}'."
            : field switch
            {
                "clientRef" => "Optional caller reference used in deterministic result mappings.",
                "pageId" or "targetPageId" => "Non-empty page GUID string.",
                "blockId" => "Non-empty block GUID string.",
                "parentBlockId" or "targetParentBlockId" => "Parent block GUID string or null for page level.",
                "order" or "targetOrder" => "Zero-based sibling order.",
                "rows" => "Logical table rows; each row contains a cells array.",
                "block" => "Strict block object with type, content and optional recursive children.",
                "blocks" => "Ordered array of strict block objects.",
                "patch" => "Object merged into canonical block content.",
                _ => $"Canonical {field} value."
            };

    private sealed record OperationDefinition(
        string Name,
        IReadOnlySet<string> AllowedFields,
        IReadOnlyList<JsonObject> Fields,
        JsonObject Example);
}
