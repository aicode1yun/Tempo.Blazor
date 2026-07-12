using System.ComponentModel.DataAnnotations;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.DataTable;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.DataTable;

/// <summary>Inline row edit: enter/commit/cancel with validation gating via the RowEditValidator slot.</summary>
public class TmDataTableInlineEditTests : LocalizationTestBase
{
    public class EditablePerson
    {
        [Required] public string Name { get; set; } = "";
        [Range(1, 150)] public int Age { get; set; }
    }

    private IRenderedComponent<TmDataTable<EditablePerson>> Render(
        List<EditablePerson> items,
        Func<EditablePerson, Task<bool>>? onCommit = null,
        bool editable = true)
        => RenderComponent<TmDataTable<EditablePerson>>(p =>
        {
            p.Add(c => c.ViewContext, "edit-test");
            p.Add(c => c.Editable, editable);
            p.Add(c => c.Items, items);
            if (onCommit is not null) p.Add(c => c.OnRowCommit, onCommit);
            p.Add(c => c.RowEditValidator, (RenderFragment)(b =>
            {
                b.OpenComponent<DataAnnotationsValidator>(0);
                b.CloseComponent();
            }));
            p.AddChildContent(b =>
            {
                var seq = 0;
                b.OpenComponent<TmDataTableColumn<EditablePerson>>(seq++);
                b.AddAttribute(seq++, "Title", "Name");
                b.AddAttribute(seq++, "Field", (Func<EditablePerson, object?>)(x => x.Name));
                b.AddAttribute(seq++, "Editable", true);
                b.AddAttribute(seq++, "EditTemplate", (RenderFragment<EditablePerson>)(item => b2 => b2.AddContent(0, item.Name)));
                b.CloseComponent();
                b.OpenComponent<TmDataTableColumn<EditablePerson>>(seq++);
                b.AddAttribute(seq++, "Title", "Age");
                b.AddAttribute(seq++, "Field", (Func<EditablePerson, object?>)(x => x.Age));
                b.CloseComponent();
            });
        });

    [Fact]
    public void DoubleClickRow_EntersEditMode()
    {
        var cut = Render([new EditablePerson { Name = "Ann", Age = 30 }]);

        cut.FindAll("tbody tr")[0].TriggerEvent("ondblclick", new MouseEventArgs());

        cut.FindAll("[data-testid='row-editing']").Should().ContainSingle();
        cut.FindAll("[data-testid='edit-commit']").Should().ContainSingle();
    }

    [Fact]
    public void NotEditable_DoubleClick_DoesNothing()
    {
        var cut = Render([new EditablePerson { Name = "Ann", Age = 30 }], editable: false);

        cut.FindAll("tbody tr")[0].TriggerEvent("ondblclick", new MouseEventArgs());

        cut.FindAll("[data-testid='row-editing']").Should().BeEmpty();
    }

    [Fact]
    public async Task CommitInvalid_StaysInEdit_AndDoesNotPersist()
    {
        var person = new EditablePerson { Name = "Ann", Age = 999 }; // Age out of range
        var committed = false;
        var cut = Render([person], _ => { committed = true; return Task.FromResult(true); });

        await cut.InvokeAsync(() => cut.Instance.BeginRowEdit(person));
        cut.Render();
        cut.Find("[data-testid='edit-commit']").Click();

        committed.Should().BeFalse();
        cut.FindAll("[data-testid='row-editing']").Should().ContainSingle();
        cut.FindAll("[data-testid='edit-error']").Should().ContainSingle();
    }

    [Fact]
    public async Task CommitValid_CallsCommitAndExitsEdit()
    {
        var person = new EditablePerson { Name = "Ann", Age = 30 };
        EditablePerson? committed = null;
        var cut = Render([person], p => { committed = p; return Task.FromResult(true); });

        await cut.InvokeAsync(() => cut.Instance.BeginRowEdit(person));
        cut.Render();
        cut.Find("[data-testid='edit-commit']").Click();

        committed.Should().BeSameAs(person);
        cut.FindAll("[data-testid='row-editing']").Should().BeEmpty();
    }

    [Fact]
    public async Task CommitRejectedByServer_StaysInEdit()
    {
        var person = new EditablePerson { Name = "Ann", Age = 30 };
        var cut = Render([person], _ => Task.FromResult(false)); // server rejects

        await cut.InvokeAsync(() => cut.Instance.BeginRowEdit(person));
        cut.Render();
        cut.Find("[data-testid='edit-commit']").Click();

        cut.FindAll("[data-testid='row-editing']").Should().ContainSingle();
    }

    public record DupPerson(string Name);

    [Fact]
    public void DuplicateValueEqualRows_OnlyClickedRowEntersEdit()
    {
        var items = new List<DupPerson> { new("Same"), new("Same"), new("Other") };
        var cut = RenderComponent<TmDataTable<DupPerson>>(p =>
        {
            p.Add(c => c.ViewContext, "dup-test");
            p.Add(c => c.Editable, true);
            p.Add(c => c.Items, items);
            p.AddChildContent(b =>
            {
                var seq = 0;
                b.OpenComponent<TmDataTableColumn<DupPerson>>(seq++);
                b.AddAttribute(seq++, "Title", "Name");
                b.AddAttribute(seq++, "Field", (Func<DupPerson, object?>)(x => x.Name));
                b.AddAttribute(seq++, "Editable", true);
                b.AddAttribute(seq++, "EditTemplate", (RenderFragment<DupPerson>)(item => b2 => b2.AddContent(0, item.Name)));
                b.CloseComponent();
            });
        });

        // The two "Same" rows are value-equal records; double-clicking the first must edit ONLY it.
        cut.FindAll("tbody tr")[0].TriggerEvent("ondblclick", new MouseEventArgs());

        cut.FindAll("[data-testid='row-editing']").Should().ContainSingle();
    }

    [Fact]
    public async Task Cancel_ExitsEditMode_WithoutCommit()
    {
        var person = new EditablePerson { Name = "Ann", Age = 30 };
        var committed = false;
        var cut = Render([person], _ => { committed = true; return Task.FromResult(true); });

        await cut.InvokeAsync(() => cut.Instance.BeginRowEdit(person));
        cut.Render();
        cut.Find("[data-testid='edit-cancel']").Click();

        committed.Should().BeFalse();
        cut.FindAll("[data-testid='row-editing']").Should().BeEmpty();
    }
}
