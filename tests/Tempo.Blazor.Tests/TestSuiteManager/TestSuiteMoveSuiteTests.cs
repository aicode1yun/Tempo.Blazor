using FluentAssertions;
using Tempo.Blazor.Demo.SharedUI.Pages.TestSuiteManager;

namespace Tempo.Blazor.Tests.TestSuiteManager;

/// <summary>Unit tests for TestSuiteDataStore.MoveSuite.</summary>
public class TestSuiteMoveSuiteTests
{
    // ── Reparent child → different parent ────────────────────────

    [Fact]
    public void MoveSuite_Child_Under_DifferentParent_UpdatesParentId()
    {
        var store = new TestSuiteDataStore();

        store.MoveSuite("suite-login", "suite-api");

        var moved = store.GetRootSuites().SelectMany(Flatten).First(s => s.Id == "suite-login");
        moved.ParentId.Should().Be("suite-api");
    }

    [Fact]
    public void MoveSuite_Child_Appears_In_NewParents_Children()
    {
        var store = new TestSuiteDataStore();

        store.MoveSuite("suite-login", "suite-api");

        var api = store.GetRootSuites().First(s => s.Id == "suite-api");
        api.Children.Should().Contain(c => c.Id == "suite-login");
    }

    [Fact]
    public void MoveSuite_Child_Removed_From_OldParents_Children()
    {
        var store = new TestSuiteDataStore();

        store.MoveSuite("suite-login", "suite-api");

        var auth = store.GetRootSuites().First(s => s.Id == "suite-auth");
        auth.Children.Should().NotContain(c => c.Id == "suite-login");
    }

    // ── Promote child → root ──────────────────────────────────────

    [Fact]
    public void MoveSuite_Child_ToRoot_SetsParentIdNull()
    {
        var store = new TestSuiteDataStore();

        store.MoveSuite("suite-login", null);

        var moved = store.GetRootSuites().SelectMany(Flatten).First(s => s.Id == "suite-login");
        moved.ParentId.Should().BeNull();
    }

    [Fact]
    public void MoveSuite_Child_ToRoot_AppearsInRootSuites()
    {
        var store = new TestSuiteDataStore();

        store.MoveSuite("suite-login", null);

        store.GetRootSuites().Should().Contain(s => s.Id == "suite-login");
    }

    [Fact]
    public void MoveSuite_Child_ToRoot_RemovedFromOriginalParent()
    {
        var store = new TestSuiteDataStore();

        store.MoveSuite("suite-login", null);

        var auth = store.GetRootSuites().First(s => s.Id == "suite-auth");
        auth.Children.Should().NotContain(c => c.Id == "suite-login");
    }

    // ── Move root → under child ───────────────────────────────────

    [Fact]
    public void MoveSuite_Root_UnderChild_UpdatesParentId()
    {
        var store = new TestSuiteDataStore();

        store.MoveSuite("suite-auth", "suite-rest");

        var moved = store.GetRootSuites().SelectMany(Flatten).First(s => s.Id == "suite-auth");
        moved.ParentId.Should().Be("suite-rest");
    }

    [Fact]
    public void MoveSuite_Root_UnderChild_DisappearsFromRootSuites()
    {
        var store = new TestSuiteDataStore();

        store.MoveSuite("suite-auth", "suite-rest");

        store.GetRootSuites().Should().NotContain(s => s.Id == "suite-auth");
    }

    // ── Guards ────────────────────────────────────────────────────

    [Fact]
    public void MoveSuite_OntoItself_DoesNothing()
    {
        var store = new TestSuiteDataStore();
        var rootsBefore = store.GetRootSuites().Select(s => s.Id).ToList();

        store.MoveSuite("suite-auth", "suite-auth");

        store.GetRootSuites().Select(s => s.Id).Should().BeEquivalentTo(rootsBefore);
    }

    [Fact]
    public void MoveSuite_OntoOwnDescendant_DoesNothing()
    {
        var store = new TestSuiteDataStore();
        // suite-auth is parent of suite-login; moving auth under login would create a cycle
        var loginBefore = store.GetRootSuites().SelectMany(Flatten).First(s => s.Id == "suite-login");
        var parentBefore = loginBefore.ParentId;

        store.MoveSuite("suite-auth", "suite-login");

        // suite-auth should still be a root
        store.GetRootSuites().Should().Contain(s => s.Id == "suite-auth");
        // suite-login's parent should be unchanged
        var loginAfter = store.GetRootSuites().SelectMany(Flatten).First(s => s.Id == "suite-login");
        loginAfter.ParentId.Should().Be(parentBefore);
    }

    [Fact]
    public void MoveSuite_SameParent_DoesNothing()
    {
        var store = new TestSuiteDataStore();
        var auth = store.GetRootSuites().First(s => s.Id == "suite-auth");
        var childrenBefore = auth.Children.Select(c => c.Id).ToList();

        // suite-login already belongs to suite-auth
        store.MoveSuite("suite-login", "suite-auth");

        auth.Children.Select(c => c.Id).Should().BeEquivalentTo(childrenBefore);
    }

    [Fact]
    public void MoveSuite_UnknownId_DoesNotThrow()
    {
        var store = new TestSuiteDataStore();
        var act = () => store.MoveSuite("does-not-exist", "suite-api");
        act.Should().NotThrow();
    }

    // ── Helper ────────────────────────────────────────────────────

    private static IEnumerable<TestSuite> Flatten(TestSuite s)
    {
        yield return s;
        foreach (var child in s.Children.OfType<TestSuite>())
            foreach (var d in Flatten(child))
                yield return d;
    }
}
