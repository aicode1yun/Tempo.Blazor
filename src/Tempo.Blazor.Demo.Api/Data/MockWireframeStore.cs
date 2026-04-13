using System.Text.Json;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Demo.Api.Data;

public class MockWireframeStore
{
    private readonly Dictionary<string, WireframeDocument> _store;

    public MockWireframeStore()
    {
        _store = new Dictionary<string, WireframeDocument>(StringComparer.OrdinalIgnoreCase)
        {
            ["login-page"]    = BuildLoginPage(),
            ["dashboard"]     = BuildDashboard(),
            ["custom-components"] = BuildCustomComponents()
        };
    }

    public IEnumerable<WireframeDocument> GetAll() => _store.Values;

    public WireframeDocument? Get(string slug) =>
        _store.TryGetValue(slug, out var doc) ? doc : null;

    public void Upsert(string slug, WireframeDocument doc) =>
        _store[slug] = doc;

    // ── Sample builders ────────────────────────────────────────────────────────

    private static WireframeDocument BuildLoginPage() => new()
    {
        Title  = "Login Page",
        Width  = 1280,
        Height = 800,
        Elements =
        [
            MakeEl("TmCard",        "card",       360, 240, 560, 420),
            MakeEl("TmAvatar",      "logo",       580, 200,  48,  48, ("name", "MyApp"), ("size", "xxl")),
            MakeEl("TmHeading",     "heading",    400, 272, 480,  40, ("text", "Sign in to your account"), ("level", "h2")),
            MakeEl("TmTextInput",   "email",      400, 332, 480,  40, ("label", "Email address"), ("placeholder", "you@example.com"), ("type", "email"), ("required", true)),
            MakeEl("TmTextInput",   "password",   400, 392, 480,  40, ("label", "Password"), ("type", "password"), ("required", true)),
            MakeEl("TmPasswordStrengthIndicator", "strength", 400, 440, 480, 20, ("strength", 3)),
            MakeEl("TmCheckbox",    "remember",   400, 472, 200,  24, ("label", "Remember me")),
            MakeEl("TmButton",      "submit",     400, 512, 480,  44, ("text", "Sign in"), ("variant", "primary"), ("block", true)),
            MakeEl("TmText",        "forgot",     400, 572, 480,  24, ("text", "Forgot your password?"), ("align", "center")),
            MakeEl("TmDivider",     "divider",    360, 608, 560,   1),
            MakeEl("TmButton",      "register",   400, 620, 480,  40, ("text", "Create new account"), ("variant", "secondary")),
            MakeEl("TmAlert",       "alert",      360, 674, 560,  40, ("text", "Invalid email or password."), ("type", "error"), ("title", "Error")),
            MakeEl("TmText",        "tagline",    440, 672, 400,  24, ("text", "© 2025 MyApp. All rights reserved."), ("align", "center")),
        ]
    };

    private static WireframeDocument BuildDashboard() => new()
    {
        Title  = "Dashboard",
        Width  = 1440,
        Height = 900,
        Elements =
        [
            MakeEl("TmSidebar",     "sidebar",      0,   0, 240, 900, ("collapsed", false), ("items", new[] { "Dashboard", "Orders", "Products", "Customers", "Settings" })),
            MakeEl("TmNavbar",      "navbar",     240,   0,1200,  56),
            MakeEl("TmNotificationBell", "bell",  1360,  12,  40,  32, ("unreadCount", 3)),
            MakeEl("TmAvatar",      "userAvatar",  1400,  12,  32,  32, ("name", "John Doe"), ("size", "md")),
            MakeEl("TmToolbar",     "toolbar",    240,  56,1200,  48, ("title", "Overview")),
            MakeEl("TmToolbarButton", "tbRefresh",  1320,  64,  40,  32, ("icon", "refresh-cw")),
            MakeEl("TmToolbarDivider", "tbDiv",   1370,  64,   4,  32),
            MakeEl("TmStatCard",    "stat1",      264,  120, 260,  96, ("title", "Total Users"),    ("value", "12,430"),  ("subValue", "+5% this month"),     ("subValueColor", "#22c55e")),
            MakeEl("TmStatCard",    "stat2",      544,  120, 260,  96, ("title", "Revenue"),        ("value", "$84,210"),  ("subValue", "+12% vs last month"), ("subValueColor", "#22c55e")),
            MakeEl("TmStatCard",    "stat3",      824,  120, 260,  96, ("title", "Active Orders"),  ("value", "1,284"),   ("subValue", "-3% this week"),      ("subValueColor", "#ef4444")),
            MakeEl("TmStatCard",    "stat4",     1104,  120, 260,  96, ("title", "Issues"),         ("value", "7"),       ("subValue", "0 new today"),        ("subValueColor", "#6b7280")),
            MakeEl("TmChart",       "chart",      264,  232, 720, 320, ("title", "Monthly Revenue"), ("type", "bar"), ("showLegend", true), ("showGrid", true), ("horizontal", false)),
            MakeEl("TmDataTable",   "table",      264,  568, 720, 280, ("title", "Recent Orders"), ("selectable", true), ("showColumnPicker", true), ("showFilters", true)),
            MakeEl("TmCard",        "activity",  1000, 232, 364, 280, ("title", "Recent Activity"), ("variant", "elevated")),
            MakeEl("TmAvatarGroup", "avatars",   1020, 280, 120,  40, ("max", 3), ("totalCount", 8), ("size", "sm")),
            MakeEl("TmTagPicker",   "tags",      1020, 340, 324,  60, ("tags", new[] { "urgent", "bug", "feature" }), ("allowCreate", true)),
            MakeEl("TmCard",        "tasks",     1000, 528, 364, 320, ("title", "Pending Tasks")),
            MakeEl("TmStepper",     "stepper",   1020, 580, 324,  80, ("steps", new[] { "Draft", "Review", "Publish" }), ("activeStep", 2), ("orientation", "horizontal")),
            MakeEl("TmToastContainer", "toasts", 1200, 720, 160, 120, ("position", "bottom-right"), ("maxVisible", 3)),
            MakeEl("TmBreadcrumb",  "breadcrumb", 264,  72, 600,  24),
        ]
    };

    private static WireframeDocument BuildCustomComponents() => new()
    {
        Title  = "Marketing Landing Page",
        Width  = 1280,
        Height = 900,
        Elements =
        [
            MakeEl("HeroSection",  "hero",     0,   0,  1280, 360, ("headline", "Ship faster with Tempo.Blazor"), ("subtext", "A comprehensive Blazor component library.")),
            MakeEl("FeatureCard",  "feat1",   80, 400, 340, 180, ("title", "100+ Components"), ("icon", "grid")),
            MakeEl("FeatureCard",  "feat2",  470, 400, 340, 180, ("title", "Dark Mode"), ("icon", "moon")),
            MakeEl("FeatureCard",  "feat3",  860, 400, 340, 180, ("title", "AI-Friendly"), ("icon", "sparkles")),
            MakeEl("CtaBanner",    "cta",      0, 700, 1280, 140, ("text", "Get started today"), ("buttonLabel", "View docs")),
            MakeEl("TmNavbar",     "nav",      0,   0, 1280,  56),
        ]
    };

    private static WireframeElement MakeEl(
        string type, string id,
        double x, double y, double w, double h,
        params (string Key, object Value)[] props)
    {
        var el = new WireframeElement { Id = id, Type = type, X = x, Y = y, W = w, H = h };
        foreach (var (key, value) in props)
            el.SetProp(key, value);
        return el;
    }
}
