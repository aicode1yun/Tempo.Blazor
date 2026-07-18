using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

/// <summary>
/// Phase 7 proofing contract: <see cref="DocumentProofingService.BuildOptions"/> materializes
/// asynchronous <see cref="ITempoProofingProvider"/> issues into the word-list based
/// <see cref="DocumentProofingOptions"/> the canvas proofing runtime consumes.
/// </summary>
public sealed class DocumentProofingServiceTests
{
    [Fact]
    public void BuildOptions_MaterializesIssuesIntoFlaggedWordsAndSuggestions()
    {
        var result = new DocumentProofingCheckResult
        {
            Issues =
            [
                new DocumentProofingIssue { Word = "smlouvva", Suggestions = ["smlouva"] },
                new DocumentProofingIssue { Word = "chybbou", Suggestions = ["chybou", "chybami"] }
            ]
        };

        var options = DocumentProofingService.BuildOptions(result);

        options.Enabled.Should().BeTrue();
        options.FlaggedWords.Should().BeEquivalentTo("smlouvva", "chybbou");
        options.Suggestions["smlouvva"].Should().Equal("smlouva");
        options.Suggestions["chybbou"].Should().Equal("chybou", "chybami");
    }

    [Fact]
    public void BuildOptions_MergesBaseOptionsAndKeepsHostWordLists()
    {
        var baseOptions = new DocumentProofingOptions
        {
            DefaultLanguage = "cs-CZ",
            FlaggedWords = ["wrngg"],
            Suggestions = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["wrngg"] = ["wrong"]
            }
        };
        var result = new DocumentProofingCheckResult
        {
            Issues = [new DocumentProofingIssue { Word = "smlouvva", Suggestions = ["smlouva"] }]
        };

        var options = DocumentProofingService.BuildOptions(result, baseOptions);

        options.DefaultLanguage.Should().Be("cs-CZ");
        options.FlaggedWords.Should().BeEquivalentTo("wrngg", "smlouvva");
        options.Suggestions["wrngg"].Should().Equal("wrong");
        options.Suggestions["smlouvva"].Should().Equal("smlouva");
    }

    [Fact]
    public void BuildOptions_DeduplicatesRepeatedIssuesCaseInsensitively()
    {
        var result = new DocumentProofingCheckResult
        {
            Issues =
            [
                new DocumentProofingIssue { Word = "Smlouvva", Suggestions = ["Smlouva"] },
                new DocumentProofingIssue { Word = "smlouvva", Suggestions = ["smlouva", "Smlouva"] }
            ]
        };

        var options = DocumentProofingService.BuildOptions(result);

        options.FlaggedWords.Should().ContainSingle();
        options.Suggestions.Should().ContainKey("Smlouvva")
            .WhoseValue.Should().Equal("Smlouva", "smlouva");
    }

    [Fact]
    public void BuildOptions_IgnoresIssuesWithoutAWord()
    {
        var result = new DocumentProofingCheckResult
        {
            Issues =
            [
                new DocumentProofingIssue { Word = "  " },
                new DocumentProofingIssue { Word = "chybbou" }
            ]
        };

        var options = DocumentProofingService.BuildOptions(result);

        options.FlaggedWords.Should().BeEquivalentTo("chybbou");
    }

    [Fact]
    public void BuildOptions_WithNullResult_ReturnsBaseEquivalentOptions()
    {
        var baseOptions = new DocumentProofingOptions
        {
            Enabled = false,
            DefaultLanguage = "en-US",
            FlaggedWords = ["wrngg"]
        };

        var options = DocumentProofingService.BuildOptions(null, baseOptions);

        options.Enabled.Should().BeFalse();
        options.DefaultLanguage.Should().Be("en-US");
        options.FlaggedWords.Should().BeEquivalentTo("wrngg");
    }

    [Fact]
    public void BuildOptions_IssueWithoutSuggestions_FlagsWordWithoutSuggestionEntry()
    {
        var result = new DocumentProofingCheckResult
        {
            Issues = [new DocumentProofingIssue { Word = "chybbou" }]
        };

        var options = DocumentProofingService.BuildOptions(result);

        options.FlaggedWords.Should().BeEquivalentTo("chybbou");
        options.Suggestions.Should().NotContainKey("chybbou");
    }

    [Fact]
    public void CheckResult_Empty_HasNoIssues()
    {
        DocumentProofingCheckResult.Empty.Issues.Should().BeEmpty();
    }
}
