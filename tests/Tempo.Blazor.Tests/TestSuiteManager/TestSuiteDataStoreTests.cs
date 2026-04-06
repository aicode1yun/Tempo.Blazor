using FluentAssertions;
using Tempo.Blazor.Demo.SharedUI.Pages.TestSuiteManager;

namespace Tempo.Blazor.Tests.TestSuiteManager;

/// <summary>Unit tests for TestSuiteDataStore.</summary>
public class TestSuiteDataStoreTests
{
    // ── GetRootSuites ─────────────────────────────────────────────

    [Fact]
    public void GetRootSuites_Returns_Only_Root_Suites()
    {
        var store = new TestSuiteDataStore();

        var roots = store.GetRootSuites();

        roots.Should().NotBeEmpty();
        roots.All(s => s.ParentId is null).Should().BeTrue();
    }

    [Fact]
    public void GetRootSuites_Each_Has_Children()
    {
        var store = new TestSuiteDataStore();

        var roots = store.GetRootSuites();

        roots.All(s => s.Children.Count > 0).Should().BeTrue();
    }

    [Fact]
    public void GetRootSuites_Children_Have_Correct_ParentId()
    {
        var store = new TestSuiteDataStore();

        foreach (var root in store.GetRootSuites())
        {
            foreach (var child in root.Children.Cast<TestSuite>())
                child.ParentId.Should().Be(root.Id);
        }
    }

    // ── GetTestCases ──────────────────────────────────────────────

    [Fact]
    public void GetTestCases_Returns_Cases_For_Known_Suite()
    {
        var store = new TestSuiteDataStore();
        var cases = store.GetTestCases("suite-login");

        cases.Should().NotBeEmpty();
        cases.All(c => c.SuiteId == "suite-login").Should().BeTrue();
    }

    [Fact]
    public void GetTestCases_Returns_Empty_For_Unknown_Suite()
    {
        var store = new TestSuiteDataStore();

        var cases = store.GetTestCases("does-not-exist");

        cases.Should().BeEmpty();
    }

    [Fact]
    public void GetTestCases_AllCases_HaveTitle()
    {
        var store = new TestSuiteDataStore();
        var allSuiteIds = new[] { "suite-login", "suite-pwreset", "suite-createuser",
                                  "suite-deleteuser", "suite-rest", "suite-graphql" };

        foreach (var id in allSuiteIds)
        {
            store.GetTestCases(id)
                 .All(c => !string.IsNullOrEmpty(c.Title))
                 .Should().BeTrue(because: $"suite {id} has cases with empty Title");
        }
    }

    // ── MoveTestCases ─────────────────────────────────────────────

    [Fact]
    public void MoveTestCases_Moves_To_Target_Suite()
    {
        var store = new TestSuiteDataStore();
        var casesBefore = store.GetTestCases("suite-login").ToList();
        var firstId = casesBefore[0].Id;

        store.MoveTestCases([firstId], "suite-rest");

        store.GetTestCases("suite-login")
             .Should().NotContain(c => c.Id == firstId);
        store.GetTestCases("suite-rest")
             .Should().Contain(c => c.Id == firstId);
    }

    [Fact]
    public void MoveTestCases_Moves_Multiple_Cases()
    {
        var store = new TestSuiteDataStore();
        var cases = store.GetTestCases("suite-login").Take(2).Select(c => c.Id).ToList();

        store.MoveTestCases(cases, "suite-graphql");

        foreach (var id in cases)
        {
            store.GetTestCases("suite-graphql")
                 .Should().Contain(c => c.Id == id);
        }
    }

    [Fact]
    public void MoveTestCases_NonExistentIds_DoesNotThrow()
    {
        var store = new TestSuiteDataStore();

        var act = () => store.MoveTestCases(["fake-id-1", "fake-id-2"], "suite-rest");

        act.Should().NotThrow();
    }

    [Fact]
    public void MoveTestCases_EmptyList_ChangesNothing()
    {
        var store = new TestSuiteDataStore();
        var countBefore = store.GetTestCases("suite-login").Count;

        store.MoveTestCases([], "suite-rest");

        store.GetTestCases("suite-login").Count.Should().Be(countBefore);
    }

    [Fact]
    public void MoveTestCases_ToSameSuite_LeavesCountUnchanged()
    {
        var store = new TestSuiteDataStore();
        var cases = store.GetTestCases("suite-login");
        var countBefore = cases.Count;
        var firstId = cases[0].Id;

        store.MoveTestCases([firstId], "suite-login");

        store.GetTestCases("suite-login").Count.Should().Be(countBefore);
    }

    [Fact]
    public void MoveTestCases_SourceCount_DecreasesBy_Moved()
    {
        var store = new TestSuiteDataStore();
        var sourceCases = store.GetTestCases("suite-login").ToList();
        var toMove = sourceCases.Take(2).Select(c => c.Id).ToList();
        var expectedCount = sourceCases.Count - 2;

        store.MoveTestCases(toMove, "suite-rest");

        store.GetTestCases("suite-login").Count.Should().Be(expectedCount);
    }
}
