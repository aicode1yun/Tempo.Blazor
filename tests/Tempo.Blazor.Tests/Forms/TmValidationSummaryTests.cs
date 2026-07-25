using Bunit.Rendering;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Tempo.Blazor.Components.Forms;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Forms;

public class TmValidationSummaryTests : LocalizationTestBase
{
    private IRenderedComponent<ContainerFragment> RenderWithEditContext<TModel>(TModel model, Action<ComponentParameterCollectionBuilder<TmValidationSummary>>? configure = null)
        where TModel : class
    {
        return Render(builder =>
        {
            builder.OpenComponent<EditForm>(0);
            builder.AddAttribute(1, nameof(EditForm.Model), model);
            builder.AddAttribute(2, nameof(EditForm.ChildContent),
                (RenderFragment<EditContext>)(context => childBuilder =>
                {
                    childBuilder.OpenComponent<TmValidationSummary>(0);
                    configure?.Invoke(new ComponentParameterCollectionBuilder<TmValidationSummary>());
                    childBuilder.CloseComponent();
                }));
            builder.CloseComponent();
        });
    }

    private IRenderedComponent<ContainerFragment> RenderInEditForm<TModel>(TModel model, Action<Dictionary<string, object?>>? addParams = null)
        where TModel : class
    {
        return Render(builder =>
        {
            builder.OpenComponent<EditForm>(0);
            builder.AddAttribute(1, nameof(EditForm.Model), model);
            builder.AddAttribute(2, nameof(EditForm.ChildContent),
                (RenderFragment<EditContext>)(context => childBuilder =>
                {
                    childBuilder.OpenComponent<TmValidationSummary>(0);
                    var extraParams = new Dictionary<string, object?>();
                    addParams?.Invoke(extraParams);
                    var seq = 1;
                    foreach (var (key, value) in extraParams)
                    {
                        childBuilder.AddAttribute(seq++, key, value);
                    }
                    childBuilder.CloseComponent();
                }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void ValidationSummary_NoErrors_NotVisible()
    {
        // Arrange & Act — valid model, no validation triggered
        var model = new TestModel { Name = "Valid" };
        var cut = RenderInEditForm(model);

        // Assert — component should not render the error container
        cut.FindAll(".tm-validation-summary").Should().BeEmpty();
    }

    [Fact]
    public void ValidationSummary_WithErrors_Visible()
    {
        // Arrange — model with empty required field
        var model = new TestModel();
        var cut = RenderInEditForm(model);

        // Manually add validation errors to EditContext
        var editForm = cut.FindComponent<EditForm>();
        var editContext = editForm.Instance.EditContext!;
        var messageStore = new ValidationMessageStore(editContext);
        messageStore.Add(editContext.Field(nameof(TestModel.Name)), "Name is required.");
        editContext.NotifyValidationStateChanged();

        // Assert
        var summary = cut.Find(".tm-validation-summary");
        summary.Should().NotBeNull();
    }

    [Fact]
    public void ValidationSummary_ShowsErrorsList()
    {
        var model = new TestModel();
        var cut = RenderInEditForm(model);

        var editForm = cut.FindComponent<EditForm>();
        var editContext = editForm.Instance.EditContext!;
        var messageStore = new ValidationMessageStore(editContext);
        messageStore.Add(editContext.Field(nameof(TestModel.Name)), "Name is required.");
        messageStore.Add(editContext.Field(nameof(TestModel.Email)), "Email is invalid.");
        editContext.NotifyValidationStateChanged();

        // Assert — should have 2 error items in the list
        var items = cut.FindAll(".tm-validation-summary-list li");
        items.Count.Should().Be(2);
        items[0].TextContent.Should().Contain("Name is required.");
        items[1].TextContent.Should().Contain("Email is invalid.");
    }

    [Fact]
    public void ValidationSummary_ShowErrorsList_False_HidesList()
    {
        var model = new TestModel();
        var cut = RenderInEditForm(model, p =>
        {
            p["ShowErrorsList"] = false;
        });

        var editForm = cut.FindComponent<EditForm>();
        var editContext = editForm.Instance.EditContext!;
        var messageStore = new ValidationMessageStore(editContext);
        messageStore.Add(editContext.Field(nameof(TestModel.Name)), "Name is required.");
        editContext.NotifyValidationStateChanged();

        // Assert — summary should exist but no list items
        cut.Find(".tm-validation-summary").Should().NotBeNull();
        cut.FindAll(".tm-validation-summary-list").Should().BeEmpty();
    }

    [Fact]
    public void ValidationSummary_CustomTitle_Rendered()
    {
        var model = new TestModel();
        var cut = RenderInEditForm(model, p =>
        {
            p["Title"] = "Custom Error Title";
        });

        var editForm = cut.FindComponent<EditForm>();
        var editContext = editForm.Instance.EditContext!;
        var messageStore = new ValidationMessageStore(editContext);
        messageStore.Add(editContext.Field(nameof(TestModel.Name)), "Error");
        editContext.NotifyValidationStateChanged();

        // Assert — custom title should be rendered
        cut.Find(".tm-validation-summary-title").TextContent.Should().Contain("Custom Error Title");
    }

    [Fact]
    public void ValidationSummary_DefaultTitle_FromLocalizer()
    {
        var model = new TestModel();
        var cut = RenderInEditForm(model);

        var editForm = cut.FindComponent<EditForm>();
        var editContext = editForm.Instance.EditContext!;
        var messageStore = new ValidationMessageStore(editContext);
        messageStore.Add(editContext.Field(nameof(TestModel.Name)), "Error");
        editContext.NotifyValidationStateChanged();

        // Assert — default title from localizer
        var title = cut.Find(".tm-validation-summary-title").TextContent;
        title.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ValidationSummary_ManualMode_HiddenWhenShowIsFalse()
    {
        var model = new TestModel();
        var cut = RenderInEditForm(model, p =>
        {
            p["ManualMode"] = true;
            p["Show"] = false;
        });

        var editForm = cut.FindComponent<EditForm>();
        var editContext = editForm.Instance.EditContext!;
        var messageStore = new ValidationMessageStore(editContext);
        messageStore.Add(editContext.Field(nameof(TestModel.Name)), "Error");
        editContext.NotifyValidationStateChanged();

        // Assert — ManualMode=true + Show=false → hidden even with errors
        cut.FindAll(".tm-validation-summary").Should().BeEmpty();
    }

    [Fact]
    public void ValidationSummary_ManualMode_VisibleWhenShowIsTrue()
    {
        var model = new TestModel();
        var cut = RenderInEditForm(model, p =>
        {
            p["ManualMode"] = true;
            p["Show"] = true;
        });

        var editForm = cut.FindComponent<EditForm>();
        var editContext = editForm.Instance.EditContext!;
        var messageStore = new ValidationMessageStore(editContext);
        messageStore.Add(editContext.Field(nameof(TestModel.Name)), "Error");
        editContext.NotifyValidationStateChanged();

        // Assert — ManualMode=true + Show=true + errors → visible
        cut.Find(".tm-validation-summary").Should().NotBeNull();
    }

    [Fact]
    public void ValidationSummary_CustomClass_Applied()
    {
        var model = new TestModel();
        var cut = RenderInEditForm(model, p =>
        {
            p["Class"] = "my-custom-class";
        });

        var editForm = cut.FindComponent<EditForm>();
        var editContext = editForm.Instance.EditContext!;
        var messageStore = new ValidationMessageStore(editContext);
        messageStore.Add(editContext.Field(nameof(TestModel.Name)), "Error");
        editContext.NotifyValidationStateChanged();

        // Assert
        var summary = cut.Find(".tm-validation-summary");
        summary.ClassList.Should().Contain("my-custom-class");
    }

    [Fact]
    public void ValidationSummary_HasIcon()
    {
        var model = new TestModel();
        var cut = RenderInEditForm(model);

        var editForm = cut.FindComponent<EditForm>();
        var editContext = editForm.Instance.EditContext!;
        var messageStore = new ValidationMessageStore(editContext);
        messageStore.Add(editContext.Field(nameof(TestModel.Name)), "Error");
        editContext.NotifyValidationStateChanged();

        // Assert — should have an icon
        cut.Find(".tm-validation-summary-icon").Should().NotBeNull();
    }

    [Fact]
    public void ValidationSummary_ErrorsCleared_HidesComponent()
    {
        var model = new TestModel();
        var cut = RenderInEditForm(model);

        var editForm = cut.FindComponent<EditForm>();
        var editContext = editForm.Instance.EditContext!;
        var messageStore = new ValidationMessageStore(editContext);

        // Add errors
        messageStore.Add(editContext.Field(nameof(TestModel.Name)), "Error");
        editContext.NotifyValidationStateChanged();
        cut.Find(".tm-validation-summary").Should().NotBeNull();

        // Clear errors
        messageStore.Clear();
        editContext.NotifyValidationStateChanged();

        // Assert — component should hide
        cut.FindAll(".tm-validation-summary").Should().BeEmpty();
    }

    [Fact]
    public void ValidationSummary_HasRoleAlert()
    {
        var model = new TestModel();
        var cut = RenderInEditForm(model);

        var editForm = cut.FindComponent<EditForm>();
        var editContext = editForm.Instance.EditContext!;
        var messageStore = new ValidationMessageStore(editContext);
        messageStore.Add(editContext.Field(nameof(TestModel.Name)), "Error");
        editContext.NotifyValidationStateChanged();

        // Assert — should have role="alert" for accessibility
        var summary = cut.Find(".tm-validation-summary");
        summary.GetAttribute("role").Should().Be("alert");
    }

    /// <summary>
    /// The summary is a per-FORM list, not a per-FIELD one, so the same sentence attached to two fields —
    /// which is exactly what a cross-field rule ("Fill in at least one contact") and a server response
    /// merged into the live store both produce — was printed once per field. The user then reads the same
    /// instruction twice and cannot tell whether two things are wrong or one.
    /// </summary>
    [Fact]
    public void ValidationSummary_SameMessageOnTwoFields_IsListedOnce()
    {
        var model = new TestModel();
        var cut = RenderInEditForm(model);

        var editForm = cut.FindComponent<EditForm>();
        var editContext = editForm.Instance.EditContext!;
        var messageStore = new ValidationMessageStore(editContext);
        messageStore.Add(editContext.Field(nameof(TestModel.Name)), "Fill in at least one contact.");
        messageStore.Add(editContext.Field(nameof(TestModel.Email)), "Fill in at least one contact.");
        editContext.NotifyValidationStateChanged();

        var items = cut.FindAll(".tm-validation-summary-list li");
        items.Count.Should().Be(1, "the same sentence twice tells the user nothing the first one did not");
        items[0].TextContent.Should().Contain("Fill in at least one contact.");
    }

    /// <summary>
    /// The de-duplication must not swallow genuinely different messages, and must not reorder them: the
    /// list is read top to bottom and the first entry is the one a screen reader announces first.
    /// </summary>
    [Fact]
    public void ValidationSummary_KeepsDistinctMessages_InOrder()
    {
        var model = new TestModel();
        var cut = RenderInEditForm(model);

        var editForm = cut.FindComponent<EditForm>();
        var editContext = editForm.Instance.EditContext!;
        var messageStore = new ValidationMessageStore(editContext);
        messageStore.Add(editContext.Field(nameof(TestModel.Name)), "Name is required.");
        messageStore.Add(editContext.Field(nameof(TestModel.Name)), "Name is required.");
        messageStore.Add(editContext.Field(nameof(TestModel.Email)), "Email is invalid.");
        editContext.NotifyValidationStateChanged();

        var items = cut.FindAll(".tm-validation-summary-list li");
        items.Count.Should().Be(2);
        items[0].TextContent.Should().Contain("Name is required.");
        items[1].TextContent.Should().Contain("Email is invalid.");
    }

    /// <summary>
    /// Visibility keys off the same collection the list renders, so it has to survive the de-duplication:
    /// a summary that hides itself because "one duplicate is not really an error" would be a data-loss bug
    /// dressed as a cosmetic one.
    /// </summary>
    [Fact]
    public void ValidationSummary_StaysVisible_WhenTheOnlyErrorIsDuplicated()
    {
        var model = new TestModel();
        var cut = RenderInEditForm(model);

        var editForm = cut.FindComponent<EditForm>();
        var editContext = editForm.Instance.EditContext!;
        var messageStore = new ValidationMessageStore(editContext);
        messageStore.Add(editContext.Field(nameof(TestModel.Name)), "Duplicated.");
        messageStore.Add(editContext.Field(nameof(TestModel.Email)), "Duplicated.");
        editContext.NotifyValidationStateChanged();

        cut.Find(".tm-validation-summary").Should().NotBeNull();
    }

    private class TestModel
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
