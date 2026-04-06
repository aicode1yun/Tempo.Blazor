using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using Tempo.Blazor.Components.Forms;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Forms;

public class TmFormFieldTests : LocalizationTestBase
{
    [Fact]
    public void FormField_RendersLabel()
    {
        var cut = RenderComponent<TmFormField>(p => p.Add(c => c.Label, "Email"));

        cut.Find("label.tm-form-field-label").TextContent.Should().Contain("Email");
    }

    [Fact]
    public void FormField_Required_RendersAsterisk()
    {
        var cut = RenderComponent<TmFormField>(p => p
            .Add(c => c.Label,    "Name")
            .Add(c => c.Required, true));

        cut.Find(".tm-form-field-required").Should().NotBeNull();
    }

    [Fact]
    public void FormField_HelpText_Rendered()
    {
        var cut = RenderComponent<TmFormField>(p => p
            .Add(c => c.Label,    "Password")
            .Add(c => c.HelpText, "Must be at least 8 characters."));

        cut.Find(".tm-form-field-help").TextContent.Should().Contain("Must be at least 8 characters.");
    }

    [Fact]
    public void FormField_ErrorMessage_Rendered()
    {
        var cut = RenderComponent<TmFormField>(p => p
            .Add(c => c.Label,        "Email")
            .Add(c => c.ErrorMessage, "Invalid email address."));

        cut.Find(".tm-form-field-error").TextContent.Should().Contain("Invalid email address.");
    }

    [Fact]
    public void FormField_ErrorMessage_HasErrorClass()
    {
        var cut = RenderComponent<TmFormField>(p => p
            .Add(c => c.Label,        "Email")
            .Add(c => c.ErrorMessage, "Required"));

        cut.Find(".tm-form-field").ClassList.Should().Contain("tm-form-field--error");
    }

    [Fact]
    public void FormField_ValidationFor_ShowsEditContextError()
    {
        var model = new TestModel();
        var editContext = new EditContext(model);

        var cut = RenderComponent<TmFormField>(p => p
            .Add(c => c.Label, "Name")
            .AddCascadingValue(editContext)
            .Add(c => c.ValidationFor, () => model.Name));

        var messages = new ValidationMessageStore(editContext);
        messages.Add(editContext.Field(nameof(TestModel.Name)), "Name is required.");
        editContext.NotifyValidationStateChanged();

        cut.Find(".tm-form-field-error").TextContent.Should().Contain("Name is required.");
        cut.Find(".tm-form-field").ClassList.Should().Contain("tm-form-field--error");
    }

    [Fact]
    public void FormField_ValidationFor_ErrorClearsAfterValidation()
    {
        var model = new TestModel();
        var editContext = new EditContext(model);

        var cut = RenderComponent<TmFormField>(p => p
            .Add(c => c.Label, "Name")
            .AddCascadingValue(editContext)
            .Add(c => c.ValidationFor, () => model.Name));

        var messages = new ValidationMessageStore(editContext);
        messages.Add(editContext.Field(nameof(TestModel.Name)), "Name is required.");
        editContext.NotifyValidationStateChanged();

        messages.Clear();
        editContext.NotifyValidationStateChanged();

        cut.FindAll(".tm-form-field-error").Should().BeEmpty();
        cut.Find(".tm-form-field").ClassList.Should().NotContain("tm-form-field--error");
    }

    [Fact]
    public void FormField_ExplicitErrorMessage_TakesPrecedenceOverEditContext()
    {
        var model = new TestModel();
        var editContext = new EditContext(model);

        var cut = RenderComponent<TmFormField>(p => p
            .Add(c => c.Label,        "Name")
            .Add(c => c.ErrorMessage, "Explicit error")
            .AddCascadingValue(editContext)
            .Add(c => c.ValidationFor, () => model.Name));

        var messages = new ValidationMessageStore(editContext);
        messages.Add(editContext.Field(nameof(TestModel.Name)), "EditContext error.");
        editContext.NotifyValidationStateChanged();

        cut.Find(".tm-form-field-error").TextContent.Should().Contain("Explicit error");
    }

    [Fact]
    public void FormField_HelpText_HiddenWhenEditContextErrorPresent()
    {
        var model = new TestModel();
        var editContext = new EditContext(model);

        var cut = RenderComponent<TmFormField>(p => p
            .Add(c => c.Label,    "Name")
            .Add(c => c.HelpText, "Some help")
            .AddCascadingValue(editContext)
            .Add(c => c.ValidationFor, () => model.Name));

        var messages = new ValidationMessageStore(editContext);
        messages.Add(editContext.Field(nameof(TestModel.Name)), "Name is required.");
        editContext.NotifyValidationStateChanged();

        cut.FindAll(".tm-form-field-help").Should().BeEmpty();
    }

    private class TestModel
    {
        public string Name { get; set; } = string.Empty;
    }
}
