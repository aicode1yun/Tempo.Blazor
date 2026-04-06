namespace Tempo.Blazor.Demo.SharedUI.Pages.TestSuiteManager;

/// <summary>
/// In-memory fake data store for the Test Suite Manager demo.
/// </summary>
public class TestSuiteDataStore
{
    private readonly List<TestSuite> _suites;
    private readonly List<TestCase>  _cases;

    public TestSuiteDataStore()
    {
        _suites = BuildSuites();
        _cases  = BuildTestCases();
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>Returns root-level test suites (with their children already attached).</summary>
    public IReadOnlyList<TestSuite> GetRootSuites() =>
        _suites.Where(s => s.ParentId is null).ToList();

    /// <summary>Returns all test cases belonging to the given suite.</summary>
    public IReadOnlyList<TestCase> GetTestCases(string suiteId) =>
        _cases.Where(c => c.SuiteId == suiteId).ToList();

    /// <summary>Moves the specified test cases to a different suite.</summary>
    public void MoveTestCases(IEnumerable<string> caseIds, string targetSuiteId)
    {
        var idSet = new HashSet<string>(caseIds);
        foreach (var tc in _cases.Where(c => idSet.Contains(c.Id)))
            tc.SuiteId = targetSuiteId;
    }

    /// <summary>
    /// Reparents a suite under a new parent (or promotes it to root when
    /// <paramref name="newParentId"/> is <c>null</c>).
    /// Silently ignores no-ops (same parent) and cycle attempts.
    /// </summary>
    public void MoveSuite(string suiteId, string? newParentId)
    {
        // Guard: cannot move onto itself
        if (suiteId == newParentId) return;

        var suite = _suites.FirstOrDefault(s => s.Id == suiteId);
        if (suite is null) return;

        // Guard: cannot move onto own descendant (would create a cycle)
        if (newParentId is not null && IsDescendant(suiteId, newParentId)) return;

        // No-op: already has the desired parent
        if (suite.ParentId == newParentId) return;

        // Remove from current parent's children list
        var oldParent = suite.ParentId is not null
            ? _suites.FirstOrDefault(s => s.Id == suite.ParentId)
            : null;
        oldParent?.RemoveChild(suite);

        // Attach to new parent or promote to root
        suite.ParentId = newParentId;
        if (newParentId is not null)
        {
            var newParent = _suites.FirstOrDefault(s => s.Id == newParentId);
            newParent?.AddChild(suite);
            // New parent is now no longer a leaf
            if (newParent is not null)
                newParent.IsLeaf = false;
        }
    }

    /// <summary>Returns true if <paramref name="candidateDescendantId"/> is a descendant of <paramref name="ancestorId"/>.</summary>
    private bool IsDescendant(string ancestorId, string candidateDescendantId)
    {
        var visited = new HashSet<string>();
        var current = _suites.FirstOrDefault(s => s.Id == candidateDescendantId);
        while (current?.ParentId is not null)
        {
            if (!visited.Add(current.ParentId)) break; // cycle guard
            if (current.ParentId == ancestorId) return true;
            current = _suites.FirstOrDefault(s => s.Id == current.ParentId);
        }
        return false;
    }

    // ── Seed data ─────────────────────────────────────────────────────────────

    private static List<TestSuite> BuildSuites()
    {
        // Suite hierarchy:
        //
        // Authentication (root)
        //   ├─ Login Flow
        //   └─ Password Reset
        // User Management (root)
        //   ├─ Create User
        //   └─ Delete User
        // API (root)
        //   ├─ REST Endpoints
        //   └─ GraphQL

        var login          = Suite("suite-login",    "Login Flow",       "suite-auth",   isLeaf: true);
        var passwordReset  = Suite("suite-pwreset",  "Password Reset",   "suite-auth",   isLeaf: true);
        var auth           = Suite("suite-auth",     "Authentication",   null,           isLeaf: false);
        auth.AddChild(login);
        auth.AddChild(passwordReset);

        var createUser     = Suite("suite-createuser", "Create User",    "suite-users",  isLeaf: true);
        var deleteUser     = Suite("suite-deleteuser", "Delete User",    "suite-users",  isLeaf: true);
        var users          = Suite("suite-users",    "User Management",  null,           isLeaf: false);
        users.AddChild(createUser);
        users.AddChild(deleteUser);

        var rest           = Suite("suite-rest",     "REST Endpoints",   "suite-api",    isLeaf: true);
        var graphql        = Suite("suite-graphql",  "GraphQL",          "suite-api",    isLeaf: true);
        var api            = Suite("suite-api",      "API",              null,           isLeaf: false);
        api.AddChild(rest);
        api.AddChild(graphql);

        return [auth, users, api, login, passwordReset, createUser, deleteUser, rest, graphql];
    }

    private static List<TestCase> BuildTestCases()
    {
        var tagRegression = new TestTag("tag-reg",  "Regression", "#3b82f6");
        var tagSmoke      = new TestTag("tag-smoke", "Smoke",     "#8b5cf6");
        var tagCritical   = new TestTag("tag-crit",  "Critical",  "#ef4444");
        var tagUi         = new TestTag("tag-ui",    "UI",        "#06b6d4");
        var tagApi        = new TestTag("tag-api",   "API",       "#10b981");

        return
        [
            // Login Flow
            TC("tc-001", "Valid credentials login",          "suite-login",    TestStatus.Pass,    [tagSmoke, tagCritical], "Verify user can log in with correct username and password"),
            TC("tc-002", "Invalid password shows error",     "suite-login",    TestStatus.Pass,    [tagRegression],         "Error message appears on wrong password"),
            TC("tc-003", "Account locked after 5 attempts",  "suite-login",    TestStatus.Fail,    [tagCritical],           "Account locks after 5 failed login attempts"),
            TC("tc-004", "Remember me persists session",     "suite-login",    TestStatus.NotRun,  [tagRegression],         "Session persists across browser restarts when remember me is checked"),
            TC("tc-005", "Login redirect after auth",        "suite-login",    TestStatus.Pass,    [tagUi],                 "User is redirected to dashboard after successful login"),

            // Password Reset
            TC("tc-006", "Reset email is sent",              "suite-pwreset",  TestStatus.Pass,    [tagRegression],         "Password reset email is delivered within 60 seconds"),
            TC("tc-007", "Reset link expires after 1 hour",  "suite-pwreset",  TestStatus.Pass,    [tagCritical],           "Expired reset link shows appropriate error"),
            TC("tc-008", "New password must differ",         "suite-pwreset",  TestStatus.Skipped, [tagRegression],         "Cannot reuse the last 5 passwords"),

            // Create User
            TC("tc-009", "Create user with all fields",      "suite-createuser", TestStatus.Pass,  [tagSmoke],              "All mandatory and optional fields are saved correctly"),
            TC("tc-010", "Duplicate email is rejected",      "suite-createuser", TestStatus.Pass,  [tagCritical],           "System prevents creation with an existing email address"),
            TC("tc-011", "Invalid email format validation",  "suite-createuser", TestStatus.Fail,  [tagRegression, tagUi],  "Inline validation fires on blur for email field"),
            TC("tc-012", "Assign role during creation",      "suite-createuser", TestStatus.NotRun,[tagRegression],         "User is assigned to the selected role on save"),

            // Delete User
            TC("tc-013", "Soft delete removes from list",    "suite-deleteuser", TestStatus.Pass,  [tagSmoke],              "Deleted user no longer appears in active user list"),
            TC("tc-014", "Delete confirmation dialog",       "suite-deleteuser", TestStatus.Pass,  [tagUi],                 "Confirmation dialog prevents accidental deletion"),
            TC("tc-015", "Cannot delete own account",        "suite-deleteuser", TestStatus.Pass,  [tagCritical],           "Logged-in user cannot delete their own account"),

            // REST Endpoints
            TC("tc-016", "GET /users returns 200",           "suite-rest",     TestStatus.Pass,    [tagApi, tagSmoke],      "Endpoint returns HTTP 200 with valid pagination"),
            TC("tc-017", "POST /users creates resource",     "suite-rest",     TestStatus.Pass,    [tagApi],                "Resource is persisted and 201 is returned"),
            TC("tc-018", "PUT /users/{id} updates fields",   "suite-rest",     TestStatus.Fail,    [tagApi, tagRegression], "Partial update via PUT preserves unset fields"),
            TC("tc-019", "DELETE /users/{id} returns 204",   "suite-rest",     TestStatus.NotRun,  [tagApi],                "Delete endpoint returns 204 No Content"),
            TC("tc-020", "401 on missing bearer token",      "suite-rest",     TestStatus.Pass,    [tagApi, tagCritical],   "Unauthenticated requests receive 401 Unauthorized"),

            // GraphQL
            TC("tc-021", "Query users list",                 "suite-graphql",  TestStatus.Pass,    [tagApi, tagSmoke],      "users query returns correct fields and pagination"),
            TC("tc-022", "Mutation createUser",              "suite-graphql",  TestStatus.Pass,    [tagApi],                "createUser mutation creates and returns new user"),
            TC("tc-023", "Introspection is disabled in prod","suite-graphql",  TestStatus.Skipped, [tagCritical],           "Introspection query returns error in production mode"),
        ];
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TestSuite Suite(string id, string label, string? parentId, bool isLeaf) =>
        new() { Id = id, Label = label, ParentId = parentId, IsLeaf = isLeaf };

    private static TestCase TC(
        string id,
        string title,
        string suiteId,
        string status,
        IReadOnlyList<TestTag> tags,
        string? description = null) =>
        new()
        {
            Id          = id,
            Title       = title,
            SubTitle    = description,
            SuiteId     = suiteId,
            StatusLabel = status,
            StatusColor = TestStatus.ColorFor(status),
            Tags        = tags,
            Date        = DateTimeOffset.UtcNow.AddDays(-Random.Shared.Next(1, 90)),
        };
}
