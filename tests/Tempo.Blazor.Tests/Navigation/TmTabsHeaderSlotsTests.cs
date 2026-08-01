using System.Security.Cryptography;
using System.Text;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Navigation;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Navigation;

/// <summary>
/// Guards the HeaderLeading/HeaderTrailing slots of <see cref="TmTabs"/>.
/// <para>
/// The whole safety argument for adding the slots is "with no slot the markup does not change at
/// all", so it is MEASURED here rather than argued: <see cref="NoSlotMarkupIsByteIdenticalToTheFrozenPreSlotBaseline"/>
/// compares a sha256 of the rendered markup against a hash frozen BEFORE the slots existed.
/// </para>
/// </summary>
public class TmTabsHeaderSlotsTests : LocalizationTestBase
{
    /// <summary>
    /// sha256 of <see cref="RenderNoSlots"/>'s markup, measured on the pre-slot TmTabs.razor
    /// (Tempo main @ ab4a6851, before HeaderLeading/HeaderTrailing were added). 1269 bytes.
    /// <para>
    /// DO NOT recompute this from the current implementation — that would make the test vacuous.
    /// It is the reference that proves existing consumers see an unchanged DOM, and it is the
    /// reason the wrapper row must stay CONDITIONAL: emitting the row unconditionally moves the
    /// strip one level down and this hash changes.
    /// </para>
    /// </summary>
    private const string PreSlotMarkupSha256 =
        "9ed0c8d568723921558b342e9f54c02c47648ab880369de2b7e01a35b5c572cf";

    // ── The byte-identity guarantee ────────────────────────

    [Fact]
    public void NoSlotMarkupIsByteIdenticalToTheFrozenPreSlotBaseline()
    {
        var markup = RenderNoSlots().Markup;
        var bytes = Encoding.UTF8.GetBytes(markup);
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        sha.Should().Be(
            PreSlotMarkupSha256,
            "TmTabs without HeaderLeading/HeaderTrailing must render the pre-slot markup byte for byte "
            + "(measured {0} bytes, expected 1269); rendered markup was:\n{1}",
            bytes.Length,
            markup);
    }

    [Fact]
    public void WithoutSlotsNoHeaderRowWrapperIsEmitted()
    {
        var cut = RenderNoSlots();

        cut.FindAll(".tm-tabs__header-row").Should().BeEmpty(
            "the wrapper row is what would push .tm-tabs__header out of being a DIRECT child of .tm-tabs, "
            + "breaking every consumer selector shaped `.tm-tabs > .tm-tabs__header`");
    }

    [Fact]
    public void WithoutSlotsTheHeaderIsADirectChildOfTheTabsRoot()
    {
        var cut = RenderNoSlots();

        var header = cut.Find(".tm-tabs__header");
        header.ParentElement!.ClassList.Should().Contain(
            "tm-tabs",
            "consumer CSS and e2e selectors use the child combinator `.tm-tabs > .tm-tabs__header`");
    }

    // ── The wrapper appears when — and only when — a slot is used ──

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void WithASlotTheHeaderRowWrapperIsEmitted(bool leading, bool trailing)
    {
        var cut = RenderWithSlots(leading, trailing);

        cut.FindAll(".tm-tabs__header-row").Should().ContainSingle(
            "supplying HeaderLeading={0}/HeaderTrailing={1} must wrap the strip in exactly one row, "
            + "otherwise the slot content has nothing to sit next to",
            leading,
            trailing);

        cut.Find(".tm-tabs__header").ParentElement!.ClassList.Should().Contain("tm-tabs__header-row");
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void OnlyTheSuppliedSlotIsEmitted(bool leading, bool trailing)
    {
        var cut = RenderWithSlots(leading, trailing);

        cut.FindAll(".tm-tabs__header-leading").Should().HaveCount(leading ? 1 : 0);
        cut.FindAll(".tm-tabs__header-trailing").Should().HaveCount(trailing ? 1 : 0);
    }

    // ── a11y: slots are SIBLINGS of role=tablist, never descendants ──

    [Theory]
    [InlineData(".tm-tabs__header-leading")]
    [InlineData(".tm-tabs__header-trailing")]
    public void SlotContentIsNotInsideTheTablist(string slotSelector)
    {
        var cut = RenderWithSlots(leading: true, trailing: true);

        var slot = cut.Find(slotSelector);

        slot.Closest("[role='tablist']").Should().BeNull(
            "{0} must be a SIBLING of the tab strip, not a descendant: TmTabs runs a roving tabindex "
            + "(arrow keys, tabindex 0/-1, aria-selected) and an element inside the tablist that can "
            + "never be selected both misreports the strip's contract to assistive technology and "
            + "becomes arrow-key reachable",
            slotSelector);

        slot.ParentElement!.ClassList.Should().Contain(
            "tm-tabs__header-row",
            "the slot's parent must be the wrapper row — that is what makes it a sibling of the strip");
    }

    // ── Document ORDER: leading BEFORE the strip, trailing AFTER it ──
    //
    // Sibling-hood is NOT the whole contract. The XML doc on HeaderLeading promises the slot is
    // "rendered immediately BEFORE the tab strip" and HeaderTrailing "immediately AFTER" it, and
    // consumers build on that: reading order, DOM order and tab order are the same order, so a
    // back-affordance in HeaderLeading must be reached before the first tab. Swapping the two
    // `@if` branches in TmTabs' HeaderRow leaves EVERY assertion above green — both slots stay
    // siblings of role=tablist and stay children of the wrapper row — so order needs its own
    // tooth. Each side is asserted separately, so a red run names which side moved.

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheLeadingSlotIsRenderedImmediatelyBeforeTheTablist(bool trailing)
    {
        var cut = RenderWithSlots(leading: true, trailing: trailing);

        var tablist = cut.Find("[role='tablist']");
        var previous = tablist.PreviousElementSibling;

        previous.Should().NotBeNull(
            "HeaderLeading is documented as rendered immediately BEFORE the tab strip, so the strip "
            + "must not be the FIRST element of .tm-tabs__header-row");

        previous!.ClassList.Should().Contain(
            "tm-tabs__header-leading",
            "the element immediately PRECEDING role=tablist must be the LEADING slot, but it was "
            + "'{0}' — DOM order is tab order, and a leading affordance rendered after the strip "
            + "is reached after every tab instead of before the first one",
            previous.ClassName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheTrailingSlotIsRenderedImmediatelyAfterTheTablist(bool leading)
    {
        var cut = RenderWithSlots(leading: leading, trailing: true);

        var tablist = cut.Find("[role='tablist']");
        var next = tablist.NextElementSibling;

        next.Should().NotBeNull(
            "HeaderTrailing is documented as rendered immediately AFTER the tab strip, so the strip "
            + "must not be the LAST element of .tm-tabs__header-row");

        next!.ClassList.Should().Contain(
            "tm-tabs__header-trailing",
            "the element immediately FOLLOWING role=tablist must be the TRAILING slot, but it was "
            + "'{0}' — a trailing action rendered before the strip is reached before the first tab",
            next.ClassName);
    }

    [Fact]
    public void TheHeaderRowRendersLeadingThenTheStripThenTrailing()
    {
        var cut = RenderWithSlots(leading: true, trailing: true);

        var row = cut.Find(".tm-tabs__header-row");
        var children = row.Children.ToArray();
        var rendered = string.Join(", ", children.Select(c => c.ClassName));

        // Class PRESENCE, not equality of the whole class attribute. What this test owns is ORDER;
        // a later modifier on any of the three (`tm-tabs__header tm-tabs__header--sticky`) would
        // break a string comparison without a single element having moved. ClassList membership is
        // token-based, so `tm-tabs__header-leading` never satisfies a check for `tm-tabs__header`.
        children.Should().HaveCount(
            3,
            "the wrapper row holds exactly the leading slot, the strip and the trailing slot; "
            + "rendered [{0}]",
            rendered);

        children[0].ClassList.Should().Contain(
            "tm-tabs__header-leading",
            "the wrapper row's children must be leading → strip → trailing in document order; "
            + "rendered [{0}]",
            rendered);

        children[1].ClassList.Should().Contain(
            "tm-tabs__header",
            "the strip must sit BETWEEN the two slots; rendered [{0}]",
            rendered);

        children[2].ClassList.Should().Contain(
            "tm-tabs__header-trailing",
            "the wrapper row's children must be leading → strip → trailing in document order; "
            + "rendered [{0}]",
            rendered);
    }

    [Fact]
    public void SlotContentDoesNotBecomeATab()
    {
        var cut = RenderWithSlots(leading: true, trailing: true);

        cut.FindAll("[role='tab']").Should().HaveCount(
            3,
            "the slots must not add anything the roving focus would walk over");
    }

    [Fact]
    public void SlotContentIsRendered()
    {
        var cut = RenderWithSlots(leading: true, trailing: true);

        cut.Find(".tm-tabs__header-leading").TextContent.Should().Contain("LEAD");
        cut.Find(".tm-tabs__header-trailing").TextContent.Should().Contain("TRAIL");
    }

    // ── Helpers ────────────────────────────────────────────

    /// <summary>
    /// Renders the exact fixture the frozen <see cref="PreSlotMarkupSha256"/> was measured from —
    /// icon, badge and disabled tab included, so the hash covers every branch of the strip.
    /// Changing this fixture invalidates the reference.
    /// </summary>
    private IRenderedComponent<TmTabs> RenderNoSlots()
        => Render<TmTabs>(p => AddPanels(p));

    private IRenderedComponent<TmTabs> RenderWithSlots(bool leading, bool trailing)
        => Render<TmTabs>(p =>
        {
            AddPanels(p);
            if (leading)
                p.Add(x => x.HeaderLeading, (RenderFragment)(b => b.AddMarkupContent(0, "<span>LEAD</span>")));
            if (trailing)
                p.Add(x => x.HeaderTrailing, (RenderFragment)(b => b.AddMarkupContent(0, "<span>TRAIL</span>")));
        });

    private static ComponentParameterCollectionBuilder<TmTabs> AddPanels(
        ComponentParameterCollectionBuilder<TmTabs> p)
        => p
            .Add(x => x.ActiveTabId, "tab1")
            .AddChildContent<TmTabPanel>(tp => tp
                .Add(x => x.Id, "tab1")
                .Add(x => x.Title, "First")
                .Add(x => x.Icon, "info")
                .AddChildContent("Content One"))
            .AddChildContent<TmTabPanel>(tp => tp
                .Add(x => x.Id, "tab2")
                .Add(x => x.Title, "Second")
                .Add(x => x.Badge, "3")
                .AddChildContent("Content Two"))
            .AddChildContent<TmTabPanel>(tp => tp
                .Add(x => x.Id, "tab3")
                .Add(x => x.Title, "Third")
                .Add(x => x.Disabled, true)
                .AddChildContent("Content Three"));
}
