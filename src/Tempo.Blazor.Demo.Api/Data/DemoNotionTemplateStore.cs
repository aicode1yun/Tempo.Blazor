using System.Text.Json;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Data;

public sealed class DemoNotionTemplateStore : INotionTemplateProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IReadOnlyList<NotionTemplateDto> _templates;

    public DemoNotionTemplateStore()
    {
        _templates =
        [
            CreateTemplate(
                "blank",
                "Blank page",
                "Start with an empty page.",
                string.Empty,
                "blank",
                []),
            CreateTemplate(
                "meeting-notes",
                "Meeting notes",
                "Capture agenda, notes, and follow-up tasks in one structured page.",
                "📝",
                "team",
                [
                    Heading(1, "Meeting notes"),
                    Text("Date, attendees, and meeting purpose"),
                    Heading(2, "Agenda"),
                    List("Opening context"),
                    List("Decisions needed"),
                    Heading(2, "Action items"),
                    Todo("Owner and due date"),
                    Todo("Follow-up communication")
                ]),
            CreateTemplate(
                "decision-record",
                "Decision record",
                "Document context, options, decision, and consequences clearly.",
                "⚖️",
                "planning",
                [
                    Heading(1, "Decision record"),
                    Callout("Record the decision with enough context for future readers."),
                    Heading(2, "Context"),
                    Text("What problem are we solving and why now?"),
                    Heading(2, "Options"),
                    List("Option A"),
                    List("Option B"),
                    Heading(2, "Decision"),
                    Text("Chosen path and rationale"),
                    Heading(2, "Consequences"),
                    Todo("Communicate the decision")
                ]),
            CreateTemplate(
                "project-plan",
                "Project plan",
                "Plan milestones, scope, owners, and delivery tasks.",
                "🚀",
                "planning",
                [
                    Heading(1, "Project plan"),
                    Text("Project summary and expected outcome"),
                    Heading(2, "Milestones"),
                    Todo("Discovery complete"),
                    Todo("Implementation ready"),
                    Todo("Launch checklist signed off"),
                    Heading(2, "Risks"),
                    List("Known dependency"),
                    List("Open decision")
                ]),
            CreateTemplate(
                "retrospective",
                "Retrospective",
                "Run a balanced team retro with highlights, learnings, and improvements.",
                "🔄",
                "team",
                [
                    Heading(1, "Retrospective"),
                    Heading(2, "What went well"),
                    List("Team highlight"),
                    Heading(2, "What was difficult"),
                    List("Process friction"),
                    Heading(2, "What we will improve"),
                    Todo("Experiment for next cycle"),
                    Todo("Owner confirms follow-up")
                ])
        ];
    }

    public Task<IReadOnlyList<NotionTemplateDto>> GetTemplatesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<NotionTemplateDto>>(_templates.Select(CloneTemplate).ToList());

    public Task<NotionTemplateDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var template = _templates.FirstOrDefault(template =>
            string.Equals(template.Id, id, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(template is null ? null : CloneTemplate(template));
    }

    private static NotionTemplateDto CreateTemplate(
        string id,
        string name,
        string description,
        string iconEmoji,
        string category,
        IReadOnlyList<PageBlock> blocks)
    {
        for (var i = 0; i < blocks.Count; i++)
        {
            blocks[i].Id = Guid.NewGuid();
            blocks[i].PageId = Guid.Empty;
            blocks[i].Order = i;
        }

        return new NotionTemplateDto
        {
            Id = id,
            Name = name,
            Description = description,
            IconEmoji = iconEmoji,
            Category = category,
            Blocks = blocks
        };
    }

    private static PageBlock Heading(int level, string html) => new()
    {
        Type = level switch
        {
            1 => BlockType.Heading1,
            2 => BlockType.Heading2,
            _ => BlockType.Heading3
        },
        Content = new HeadingBlockContent { Level = level, Html = html }
    };

    private static PageBlock Text(string html) => new()
    {
        Type = BlockType.Paragraph,
        Content = new TextBlockContent { Html = html }
    };

    private static PageBlock List(string html) => new()
    {
        Type = BlockType.BulletList,
        Content = new ListBlockContent { Html = html }
    };

    private static PageBlock Todo(string html) => new()
    {
        Type = BlockType.TodoItem,
        Content = new TodoBlockContent { Html = html }
    };

    private static PageBlock Callout(string html) => new()
    {
        Type = BlockType.Callout,
        Content = new CalloutBlockContent { IconEmoji = "💡", Variant = CalloutVariant.Info, Html = html }
    };

    private static NotionTemplateDto CloneTemplate(NotionTemplateDto template)
        => JsonSerializer.Deserialize<NotionTemplateDto>(JsonSerializer.Serialize(template, JsonOptions), JsonOptions)
           ?? throw new InvalidOperationException("Unable to clone Notion template.");
}
