using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor.Commands;
using Tempo.Blazor.Components.DocumentEditor.Registry;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

/// <summary>
/// Fáze 22 (code review): (1) DocumentEditorCommandRegistry.Register() jako jediný mutátor
/// neinvalidoval _lastContextSignature — příkaz zaregistrovaný po prvním RefreshAllAsync nikdy
/// nedostal stav (GetState null → trvale disabled/skryté tlačítko), dokud se nezměnil kontext.
/// (2) DocumentEditorSnapshotCommand: N3.1 odstranil defenzivní klony a tiše změnil kontrakt
/// veřejné třídy v publikovaném balíčku — externí konzument předávající after = živý dokument
/// dostal _after == _target a redo obnovovalo aktuální stav místo zachyceného snapshotu.
/// </summary>
public sealed class DocumentEditorApiContractReviewTests
{
    // ─── Registry: Register musí shodit signature gate ───────────────────────

    [Fact]
    public async Task Register_AfterSignatureGatedRefresh_NextRefreshComputesNewCommandState()
    {
        var registry = new DocumentEditorCommandRegistry();
        registry.Register(Entry("first"));
        var context = new DocumentEditorCommandContext { HasDocument = true };
        await registry.RefreshAllAsync(context, "stable-signature");

        registry.Register(Entry("late-arrival"));
        await registry.RefreshAllAsync(context, "stable-signature");

        registry.GetState("late-arrival").Should().NotBeNull(
            "Register() mění množinu příkazů — signature gate musí spadnout, jinak pozdě registrovaný příkaz nikdy nedostane stav");
    }

    // ─── SnapshotCommand: default kontrakt = defenzivní klony ────────────────

    [Fact]
    public async Task SnapshotCommand_DefaultContract_CapturesSnapshots_EvenForLiveDocuments()
    {
        // Externí konzument (2.0.x kontrakt): after = ŽIVÝ dokument (tentýž objekt jako target).
        var target = DocumentEditorDocument.Empty("live-doc");
        target.Theme.BodyFontFamily = "After State";
        var before = DocumentEditorDocument.Empty("live-doc");
        before.Theme.BodyFontFamily = "Before State";

        var command = new DocumentEditorSnapshotCommand(target, before, after: target, "External usage");

        // Pozdější editace živého dokumentu nesmí prosáknout do zachyceného snapshotu.
        target.Theme.BodyFontFamily = "Mutated Later";

        await command.UndoAsync();
        target.Theme.BodyFontFamily.Should().Be("Before State");

        await command.ExecuteAsync();
        target.Theme.BodyFontFamily.Should().Be("After State",
            "redo musí obnovit stav zachycený při konstrukci, ne aktuální stav živého dokumentu");
    }

    [Fact]
    public async Task SnapshotCommand_AssumeOwnership_SkipsDefensiveClones()
    {
        // Interní perf cesta (N3.1): call-site předává dedikované klony a explicitně přenechává
        // vlastnictví — konstruktor nesmí klonovat znovu (mutace po konstrukci je viditelná).
        var target = DocumentEditorDocument.Empty("owned");
        var before = DocumentEditorDocument.Empty("owned");
        var after = DocumentEditorDocument.Empty("owned");
        var command = new DocumentEditorSnapshotCommand(target, before, after, "Owned", assumeOwnership: true);

        after.Theme.BodyFontFamily = "Mutated After Construction";
        await command.ExecuteAsync();
        target.Theme.BodyFontFamily.Should().Be("Mutated After Construction");
    }

    private static FuncDocumentEditorCommandEntry Entry(string name) => new(
        name, affectsData: false,
        computeEnabled: _ => true,
        execute: (_, _) => Task.CompletedTask);
}
