using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Tempo.Blazor.Components.Inputs;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Inputs;

/// <summary>TDD tests for TmDecimalInput — phase 3: EditContext / DataAnnotations validation.</summary>
public class TmDecimalInputValidationTests : LocalizationTestBase
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    private sealed class OrderModel
    {
        [Range(10, 100, ErrorMessage = "Price must be between 10 and 100.")]
        public decimal? Price { get; set; }
    }

    /// <summary>Renders TmDecimalInput bound to <paramref name="model"/>.Price inside an EditForm with DataAnnotations.</summary>
    private IRenderedFragment RenderInEditForm(OrderModel model, string? error = null)
    {
        return Render(builder =>
        {
            builder.OpenComponent<EditForm>(0);
            builder.AddAttribute(1, nameof(EditForm.Model), model);
            builder.AddAttribute(2, nameof(EditForm.ChildContent), (RenderFragment<EditContext>)(_ =>
                (RenderTreeBuilder b) =>
                {
                    b.OpenComponent<DataAnnotationsValidator>(0);
                    b.CloseComponent();

                    b.OpenComponent<TmDecimalInput>(1);
                    b.AddAttribute(2, nameof(TmDecimalInput.Culture), English);
                    b.AddAttribute(3, nameof(TmDecimalInput.Value), model.Price);
                    b.AddAttribute(4, nameof(TmDecimalInput.ValueChanged),
                        EventCallback.Factory.Create<decimal?>(this, v => model.Price = v));
                    b.AddAttribute(5, nameof(TmDecimalInput.ValueExpression),
                        (Expression<Func<decimal?>>)(() => model.Price));
                    if (error is not null)
                    {
                        b.AddAttribute(6, nameof(TmDecimalInput.Error), error);
                    }
                    b.CloseComponent();
                }));
            builder.CloseComponent();
        });
    }

    private static EditContext GetEditContext(IRenderedFragment cut) =>
        cut.FindComponent<EditForm>().Instance.EditContext!;

    /// <summary>Counts subscribers of EditContext's field-like OnValidationStateChanged event.</summary>
    private static int ValidationStateSubscriberCount(EditContext editContext)
    {
        var field = typeof(EditContext).GetField(
            nameof(EditContext.OnValidationStateChanged),
            BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull("the test relies on the field-like event backing field");

        var handler = (Delegate?)field!.GetValue(editContext);
        return handler?.GetInvocationList().Length ?? 0;
    }

    [Fact]
    public void DecimalInput_InEditForm_NotifiesFieldChanged()
    {
        var model = new OrderModel();
        var cut = RenderInEditForm(model);
        var editContext = GetEditContext(cut);

        var changedFields = new List<string>();
        editContext.OnFieldChanged += (_, e) => changedFields.Add(e.FieldIdentifier.FieldName);

        cut.Find(".tm-decimal-input__input").Change("42.50");

        model.Price.Should().Be(42.50m);
        changedFields.Should().ContainSingle().Which.Should().Be(nameof(OrderModel.Price));
    }

    [Fact]
    public void DecimalInput_FieldIdentifier_PointsAtTheBoundModel()
    {
        var model = new OrderModel();
        var cut = RenderInEditForm(model);
        var editContext = GetEditContext(cut);

        FieldIdentifier? changed = null;
        editContext.OnFieldChanged += (_, e) => changed = e.FieldIdentifier;

        cut.Find(".tm-decimal-input__input").Change("42.50");

        changed.Should().NotBeNull();
        changed!.Value.Model.Should().BeSameAs(model);
    }

    [Fact]
    public void DecimalInput_ShowsDataAnnotationsValidationMessage()
    {
        var model = new OrderModel();
        var cut = RenderInEditForm(model);

        cut.FindAll(".tm-input-error-message").Should().BeEmpty();

        cut.Find(".tm-decimal-input__input").Change("5");   // below the [Range(10, 100)] minimum

        cut.Find(".tm-input-error-message").TextContent.Should().Contain("Price must be between 10 and 100.");
        cut.Find(".tm-decimal-input").ClassList.Should().Contain("tm-decimal-input--error");
        cut.Find(".tm-decimal-input__control").ClassList.Should().Contain("tm-decimal-input__control--error");
        cut.Find(".tm-decimal-input__input").GetAttribute("aria-invalid").Should().Be("true");
    }

    [Fact]
    public void DecimalInput_ValidValue_ClearsValidationMessage()
    {
        var model = new OrderModel();
        var cut = RenderInEditForm(model);
        var input = cut.Find(".tm-decimal-input__input");

        input.Change("5");
        cut.FindAll(".tm-input-error-message").Should().NotBeEmpty();

        cut.Find(".tm-decimal-input__input").Change("50");

        cut.FindAll(".tm-input-error-message").Should().BeEmpty();
        cut.Find(".tm-decimal-input").ClassList.Should().NotContain("tm-decimal-input--error");
    }

    [Fact]
    public void DecimalInput_ExplicitError_WinsOverValidationMessage()
    {
        var model = new OrderModel();
        var cut = RenderInEditForm(model, error: "Server rejected this price.");

        cut.Find(".tm-decimal-input__input").Change("5");   // would also trip the [Range] rule

        var messages = cut.FindAll(".tm-input-error-message");
        messages.Should().ContainSingle();
        messages[0].TextContent.Should().Contain("Server rejected this price.");
        messages[0].TextContent.Should().NotContain("Price must be between");
    }

    [Fact]
    public void DecimalInput_ValidationOutsideEditForm_IsInert()
    {
        var cut = RenderComponent<TmDecimalInput>(p => p
            .Add(x => x.Culture, English)
            .Add(x => x.Value, 5m));

        cut.Find(".tm-decimal-input__input").Change("7");

        cut.FindAll(".tm-input-error-message").Should().BeEmpty("there is no EditContext to validate against");
    }

    /// <summary>Host whose Model parameter can be swapped, which makes EditForm build a fresh EditContext.</summary>
    private sealed class SwappableModelHost : ComponentBase
    {
        [Parameter] public OrderModel Model { get; set; } = new();

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            var model = Model;
            builder.OpenComponent<EditForm>(0);
            builder.AddAttribute(1, nameof(EditForm.Model), model);
            builder.AddAttribute(2, nameof(EditForm.ChildContent), (RenderFragment<EditContext>)(_ =>
                (RenderTreeBuilder b) =>
                {
                    b.OpenComponent<DataAnnotationsValidator>(0);
                    b.CloseComponent();

                    b.OpenComponent<TmDecimalInput>(1);
                    b.AddAttribute(2, nameof(TmDecimalInput.Culture), English);
                    b.AddAttribute(3, nameof(TmDecimalInput.Value), model.Price);
                    b.AddAttribute(4, nameof(TmDecimalInput.ValueChanged),
                        EventCallback.Factory.Create<decimal?>(this, v => model.Price = v));
                    b.AddAttribute(5, nameof(TmDecimalInput.ValueExpression),
                        (Expression<Func<decimal?>>)(() => model.Price));
                    b.CloseComponent();
                }));
            builder.CloseComponent();
        }
    }

    [Fact]
    public void DecimalInput_FollowsTheModel_WhenTheFormModelIsSwapped()
    {
        var cut = RenderComponent<SwappableModelHost>(p => p.Add(x => x.Model, new OrderModel()));

        var replacement = new OrderModel();
        cut.SetParametersAndRender(p => p.Add(x => x.Model, replacement));

        var editContext = cut.FindComponent<EditForm>().Instance.EditContext!;
        FieldIdentifier? changed = null;
        editContext.OnFieldChanged += (_, e) => changed = e.FieldIdentifier;

        cut.Find(".tm-decimal-input__input").Change("5");   // below the [Range(10, 100)] minimum

        replacement.Price.Should().Be(5m);
        changed.Should().NotBeNull();
        changed!.Value.Model.Should().BeSameAs(replacement, "the field identifier must follow the swapped model");
        cut.Find(".tm-input-error-message").TextContent.Should().Contain("Price must be between 10 and 100.");
    }

    /// <summary>
    /// Host that cascades a hand-rolled EditContext. Unlike EditForm — which rebuilds its children
    /// when the model changes — this keeps the same TmDecimalInput instance across the swap, so the
    /// component itself has to notice that its EditContext (and therefore its model) was replaced.
    /// </summary>
    private sealed class CascadingEditContextHost : ComponentBase
    {
        private EditContext _editContext = new(new OrderModel());

        [Parameter] public OrderModel Model { get; set; } = new();

        public EditContext EditContext => _editContext;

        protected override void OnParametersSet()
        {
            if (!ReferenceEquals(_editContext.Model, Model))
            {
                _editContext = new EditContext(Model);
            }
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            var model = Model;
            builder.OpenComponent<CascadingValue<EditContext>>(0);
            builder.AddAttribute(1, nameof(CascadingValue<EditContext>.Value), _editContext);
            builder.AddAttribute(2, nameof(CascadingValue<EditContext>.IsFixed), false);
            builder.AddAttribute(3, nameof(CascadingValue<EditContext>.ChildContent), (RenderFragment)(b =>
            {
                b.OpenComponent<TmDecimalInput>(0);
                b.AddAttribute(1, nameof(TmDecimalInput.Culture), English);
                b.AddAttribute(2, nameof(TmDecimalInput.Value), model.Price);
                b.AddAttribute(3, nameof(TmDecimalInput.ValueChanged),
                    EventCallback.Factory.Create<decimal?>(this, v => model.Price = v));
                b.AddAttribute(4, nameof(TmDecimalInput.ValueExpression),
                    (Expression<Func<decimal?>>)(() => model.Price));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }

    [Fact]
    public void DecimalInput_ReboundEditContext_RetargetsTheFieldIdentifier()
    {
        var cut = RenderComponent<CascadingEditContextHost>(p => p.Add(x => x.Model, new OrderModel()));
        var original = cut.FindComponent<TmDecimalInput>().Instance;

        var replacement = new OrderModel();
        cut.SetParametersAndRender(p => p.Add(x => x.Model, replacement));

        cut.FindComponent<TmDecimalInput>().Instance.Should().BeSameAs(original,
            "a cascading value swap reuses the component — the stale field identifier is the whole point of this test");

        var editContext = cut.Instance.EditContext;
        FieldIdentifier? changed = null;
        editContext.OnFieldChanged += (_, e) => changed = e.FieldIdentifier;

        cut.Find(".tm-decimal-input__input").Change("5");

        changed.Should().NotBeNull();
        changed!.Value.Model.Should().BeSameAs(replacement, "the field identifier must be rebuilt for the new model");

        // A validation message registered against the new model must reach the input.
        var store = new ValidationMessageStore(editContext);
        store.Add(editContext.Field(nameof(OrderModel.Price)), "Price must be between 10 and 100.");
        editContext.NotifyValidationStateChanged();

        cut.Find(".tm-input-error-message").TextContent.Should().Contain("Price must be between 10 and 100.");
    }

    [Fact]
    public void DecimalInput_Dispose_UnsubscribesFromValidationStateChanged()
    {
        var model = new OrderModel();
        var cut = RenderInEditForm(model);
        var editContext = GetEditContext(cut);

        var whileRendered = ValidationStateSubscriberCount(editContext);
        whileRendered.Should().BeGreaterThan(0, "the component subscribes while it is alive");

        cut.FindComponent<TmDecimalInput>().Instance.Dispose();

        ValidationStateSubscriberCount(editContext).Should().Be(whileRendered - 1);

        // A late validation notification must not reach the disposed component.
        var act = () => editContext.NotifyValidationStateChanged();
        act.Should().NotThrow();
    }
}
