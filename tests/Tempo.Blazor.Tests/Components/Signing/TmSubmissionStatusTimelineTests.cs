using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Signing;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Signing;

public class TmSubmissionStatusTimelineTests : LocalizationTestBase
{
    [Fact]
    public void Render_MapsCoreLifecycleEvents()
    {
        var cut = Render<TmSubmissionStatusTimeline>(parameters => parameters
            .Add(p => p.Events,
            [
                CreateEvent(SigningSubmissionStatusEventType.Sent, "Alex"),
                CreateEvent(SigningSubmissionStatusEventType.Opened, "Alex"),
                CreateEvent(SigningSubmissionStatusEventType.Completed, "Alex"),
                CreateEvent(SigningSubmissionStatusEventType.Declined, "Nora")
            ]));

        cut.Markup.Should().Contain("Sent");
        cut.Markup.Should().Contain("Opened");
        cut.Markup.Should().Contain("Completed");
        cut.Markup.Should().Contain("Declined");
        cut.FindAll(".tm-submission-status-timeline__item--danger").Should().HaveCount(1);
    }

    [Fact]
    public void Render_EmailAndVerificationEventsWithMetadata()
    {
        var cut = Render<TmSubmissionStatusTimeline>(parameters => parameters
            .Add(p => p.Events,
            [
                CreateEvent(SigningSubmissionStatusEventType.EmailBounced, "Alex", new Dictionary<string, string> { ["SMTP"] = "550" }),
                CreateEvent(SigningSubmissionStatusEventType.EmailComplaint, "Nora"),
                CreateEvent(SigningSubmissionStatusEventType.VerificationCompleted, "Alex"),
                CreateEvent(SigningSubmissionStatusEventType.KbaCompleted, "Alex")
            ]));

        cut.Markup.Should().Contain("Email bounced");
        cut.Markup.Should().Contain("Email complaint");
        cut.Markup.Should().Contain("Verification completed");
        cut.Markup.Should().Contain("KBA completed");
        cut.Markup.Should().Contain("550");
    }

    private static SigningSubmissionStatusEvent CreateEvent(
        SigningSubmissionStatusEventType type,
        string recipient,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new SigningSubmissionStatusEvent
        {
            Type = type,
            RecipientName = recipient,
            RecipientEmail = $"{recipient.ToLowerInvariant()}@example.test",
            Metadata = metadata ?? new Dictionary<string, string>()
        };
    }
}
