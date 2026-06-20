using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Helpers;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public class InlineStatusSerializationTests
{
    [Fact]
    public void InlineStatus_RoundtripsThroughTextBlockHtml()
    {
        var content = new TextBlockContent
        {
            Html = """
                   Ship when <span contenteditable="false" class="tm-notion-status tm-notion-status--green" data-status-label="DONE" data-status-color="Green"><span class="tm-notion-status__label">DONE</span></span> is approved.
                   """
        };

        var json = JsonSerializer.Serialize(content);
        var restored = JsonSerializer.Deserialize<TextBlockContent>(json);

        restored.Should().NotBeNull();
        restored!.Html.Should().Contain("tm-notion-status--green");

        var statuses = StatusParser.ExtractStatuses(restored.Html);
        statuses.Should().ContainSingle().Which.Should().BeEquivalentTo(new InlineStatus("DONE", NotionStatusColor.Green));
    }

    [Fact]
    public void InlineStatus_ParserFallsBackToClassColorAndInnerText()
    {
        const string Html = """
                            <span contenteditable="false" class="tm-notion-status tm-notion-status--purple"><span class="tm-notion-status__label">IN PROGRESS</span></span>
                            """;

        var statuses = StatusParser.ExtractStatuses(Html);

        statuses.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new InlineStatus("IN PROGRESS", NotionStatusColor.Purple));
    }

    [Fact]
    public void InlineStatus_ParserFallsBackToInnerTextWhenDataLabelIsEmpty()
    {
        const string Html = """
                            <span class="tm-notion-status tm-notion-status--blue" data-status-label="">In Review</span>
                            """;

        var statuses = StatusParser.ExtractStatuses(Html);

        statuses.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new InlineStatus("In Review", NotionStatusColor.Blue));
    }

    [Fact]
    public void InlineStatus_ParserPrefersNonEmptyDataLabelOverInnerText()
    {
        const string Html = """
                            <span class="tm-notion-status tm-notion-status--green" data-status-label="Approved">Visible text</span>
                            """;

        var statuses = StatusParser.ExtractStatuses(Html);

        statuses.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new InlineStatus("Approved", NotionStatusColor.Green));
    }
}
