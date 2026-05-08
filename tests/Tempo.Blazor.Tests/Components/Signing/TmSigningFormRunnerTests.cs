using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Signing;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Signing;

public class TmSigningFormRunnerTests : LocalizationTestBase
{
    [Fact]
    public void Render_NoFields_ShowsEmptyState()
    {
        var cut = RenderComponent<TmSigningFormRunner>();

        cut.Find(".tm-signing-form-runner__empty").TextContent.Should().Contain("No fields");
    }

    [Fact]
    public void Render_DocumentsOverlaysAndCurrentStep()
    {
        var cut = RenderComponent<TmSigningFormRunner>(parameters => parameters
            .Add(p => p.Pages, [CreatePage()])
            .Add(p => p.Fields, [CreateField("name", "Name", SigningFieldType.Text)]));

        cut.Find(".tm-document-page-viewer__page").Should().NotBeNull();
        cut.Find(".tm-signing-field-overlay").Should().NotBeNull();
        cut.Find(".tm-signing-form-runner__step-panel").TextContent.Should().Contain("Name");
    }

    [Fact]
    public void ClickOverlay_SelectsMatchingStep()
    {
        var cut = RenderComponent<TmSigningFormRunner>(parameters => parameters
            .Add(p => p.Pages, [CreatePage()])
            .Add(p => p.Fields,
            [
                CreateField("first", "First", SigningFieldType.Text, y: 0.1),
                CreateField("second", "Second", SigningFieldType.Text, y: 0.2)
            ]));

        cut.FindAll(".tm-signing-field-overlay")[1].Click();

        cut.Find(".tm-signing-form-runner__step-panel").TextContent.Should().Contain("Second");
        cut.FindAll(".tm-signing-field-overlay")[1].ClassList.Should().Contain("tm-signing-field--selected");
    }

    [Fact]
    public void Next_SavesTextAndInvokesStepSubmit()
    {
        IReadOnlyDictionary<string, object?>? values = null;
        SigningStepItem? submitted = null;
        var cut = RenderComponent<TmSigningFormRunner>(parameters => parameters
            .Add(p => p.Fields,
            [
                CreateField("name", "Name", SigningFieldType.Text),
                CreateField("date", "Date", SigningFieldType.Date, y: 0.2)
            ])
            .Add(p => p.ValuesChanged, EventCallback.Factory.Create<IReadOnlyDictionary<string, object?>>(this, v => values = v))
            .Add(p => p.OnStepSubmit, EventCallback.Factory.Create<SigningStepItem>(this, step => submitted = step)));

        cut.Find("input.tm-signing-text-step__input").Change("Alice");
        cut.Find(".tm-signing-form-runner__next").Click();

        values.Should().NotBeNull();
        values!["name"].Should().Be("Alice");
        submitted!.Field.Uuid.Should().Be("name");
        cut.Find(".tm-signing-form-runner__step-panel").TextContent.Should().Contain("Date");
    }

    [Fact]
    public void Next_RequiredMissing_StaysOnFirstInvalidField()
    {
        var cut = RenderComponent<TmSigningFormRunner>(parameters => parameters
            .Add(p => p.Fields,
            [
                CreateField("name", "Name", SigningFieldType.Text, required: true),
                CreateField("date", "Date", SigningFieldType.Date, y: 0.2)
            ]));

        cut.Find(".tm-signing-form-runner__next").Click();

        cut.Find(".tm-signing-form-runner__validation").TextContent.Should().Contain("required");
        cut.Find(".tm-signing-form-runner__step-panel").TextContent.Should().Contain("Name");
    }

    [Fact]
    public void SkipOptional_AdvancesToNextStep()
    {
        var cut = RenderComponent<TmSigningFormRunner>(parameters => parameters
            .Add(p => p.Fields,
            [
                CreateField("middle", "Middle name", SigningFieldType.Text),
                CreateField("date", "Date", SigningFieldType.Date, y: 0.2)
            ]));

        cut.Find(".tm-signing-form-runner__skip").Click();

        cut.Find(".tm-signing-form-runner__step-panel").TextContent.Should().Contain("Date");
    }

    [Fact]
    public void Complete_DisabledUntilRequiredValuesArePresent()
    {
        var cut = RenderComponent<TmSigningFormRunner>(parameters => parameters
            .Add(p => p.Fields, [CreateField("name", "Name", SigningFieldType.Text, required: true)]));

        cut.Find(".tm-signing-form-runner__complete").HasAttribute("disabled").Should().BeTrue();

        cut.Find("input.tm-signing-text-step__input").Change("Alice");

        cut.Find(".tm-signing-form-runner__complete").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void Complete_ErrorShowsValidationState()
    {
        var cut = RenderComponent<TmSigningFormRunner>(parameters => parameters
            .Add(p => p.Fields, [CreateField("name", "Name", SigningFieldType.Text, required: true)])
            .Add(p => p.OnComplete, EventCallback.Factory.Create<IReadOnlyDictionary<string, object?>>(this, _ =>
            {
                throw new InvalidOperationException("Complete service offline");
            })));

        cut.Find("input.tm-signing-text-step__input").Change("Alice");
        cut.Find(".tm-signing-form-runner__complete").Click();

        cut.Find(".tm-signing-form-runner__validation").TextContent.Should().Contain("Complete service offline");
    }

    [Fact]
    public async Task Autosave_DebouncesAndShowsError()
    {
        var autosaves = 0;
        var cut = RenderComponent<TmSigningFormRunner>(parameters => parameters
            .Add(p => p.Fields, [CreateField("name", "Name", SigningFieldType.Text)])
            .Add(p => p.AutoSaveDelay, TimeSpan.FromMilliseconds(20))
            .Add(p => p.OnAutoSave, EventCallback.Factory.Create<IReadOnlyDictionary<string, object?>>(this, _ =>
            {
                autosaves++;
                throw new InvalidOperationException("Offline");
            })));

        cut.Find("input.tm-signing-text-step__input").Change("Alice");

        cut.WaitForAssertion(() =>
        {
            autosaves.Should().Be(1);
            cut.Find(".tm-signing-form-runner__autosave").TextContent.Should().Contain("Offline");
        });
    }

    [Fact]
    public void Loading_DisablesNavigation()
    {
        var cut = RenderComponent<TmSigningFormRunner>(parameters => parameters
            .Add(p => p.Fields, [CreateField("name", "Name", SigningFieldType.Text)])
            .Add(p => p.IsLoading, true));

        cut.Find(".tm-signing-form-runner__next").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Completing_DisablesForwardNavigation()
    {
        var cut = RenderComponent<TmSigningFormRunner>(parameters => parameters
            .Add(p => p.Fields,
            [
                CreateField("name", "Name", SigningFieldType.Text),
                CreateField("date", "Date", SigningFieldType.Date, y: 0.2)
            ])
            .Add(p => p.IsCompleting, true));

        cut.Find(".tm-signing-form-runner__next").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void MobileCollapsed_CanExpandAndMinimize()
    {
        var cut = RenderComponent<TmSigningFormRunner>(parameters => parameters
            .Add(p => p.Fields, [CreateField("name", "Name", SigningFieldType.Text)])
            .Add(p => p.MobilePanelMode, TmSigningFormRunnerMobilePanelMode.Collapsed));

        cut.Find(".tm-signing-form-runner__mobile-expand").Click();
        cut.Find(".tm-signing-form-runner__mobile-panel--expanded").Should().NotBeNull();

        cut.Find(".tm-signing-form-runner__mobile-minimize").Click();
        cut.Find(".tm-signing-form-runner__mobile-panel--collapsed").Should().NotBeNull();
    }

    [Fact]
    public void MobileCompleteTarget_AddsAvoidanceClass()
    {
        var cut = RenderComponent<TmSigningFormRunner>(parameters => parameters
            .Add(p => p.Fields, [CreateField("name", "Name", SigningFieldType.Text)])
            .Add(p => p.MobileCompleteTargetSelector, "#fixed-complete"));

        cut.Find(".tm-signing-form-runner__mobile-panel").ClassList.Should().Contain("tm-signing-form-runner__mobile-panel--has-complete-target");
        cut.Find(".tm-signing-form-runner__mobile-panel").GetAttribute("data-complete-target").Should().Be("#fixed-complete");
    }

    [Fact]
    public void AccessibilityMode_ListsFieldsAndFocusesStep()
    {
        var cut = RenderComponent<TmSigningFormRunner>(parameters => parameters
            .Add(p => p.Fields,
            [
                CreateField("first", "First", SigningFieldType.Text, y: 0.1),
                CreateField("second", "Second", SigningFieldType.Text, y: 0.2)
            ]));

        cut.Find(".tm-signing-form-runner__accessibility-entry").Click();
        cut.FindAll(".tm-signing-form-runner__accessibility-field")[1].Click();

        cut.Find(".tm-signing-form-runner__step-panel").TextContent.Should().Contain("Second");
        cut.Find(".tm-signing-form-runner__progress").GetAttribute("aria-label").Should().Contain("Step");
    }

    private static SigningDocumentPage CreatePage()
    {
        return new SigningDocumentPage
        {
            AttachmentUuid = "doc",
            PageIndex = 0,
            Width = 600,
            Height = 800,
            Label = "Page 1"
        };
    }

    private static SigningField CreateField(
        string uuid,
        string name,
        SigningFieldType type,
        double y = 0.1,
        bool required = false)
    {
        return new SigningField
        {
            Uuid = uuid,
            Name = name,
            Type = type,
            Required = required,
            Areas =
            [
                new SigningFieldArea
                {
                    AttachmentUuid = "doc",
                    Page = 0,
                    X = 0.1,
                    Y = y,
                    Width = 0.2,
                    Height = 0.05
                }
            ]
        };
    }
}
