using Microsoft.AspNetCore.Components;

namespace Tempo.Blazor.Components.NotionEditor.Shared;

/// <summary>
/// Shared emoji picker popup. Renders a search input, categorised emoji grid, and optional Remove button.
/// Positioning is relative to the nearest positioned ancestor — callers must provide that context.
/// </summary>
public partial class TmNotionEmojiPicker : ComponentBase
{
    // ── Parameters ───────────────────────────────────────────────────────────

    /// <summary>Fired when the user clicks an emoji. Arg = the emoji character string.</summary>
    [Parameter] public EventCallback<string> OnSelected { get; set; }

    /// <summary>Fired when the user clicks "Remove icon".</summary>
    [Parameter] public EventCallback OnRemoved { get; set; }

    /// <summary>Fired when the backdrop is clicked (close without action).</summary>
    [Parameter] public EventCallback OnClosed { get; set; }

    /// <summary>When true, a "Remove icon" button is shown in the header.</summary>
    [Parameter] public bool ShowRemoveButton { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private string _search = string.Empty;

    // ── Computed ─────────────────────────────────────────────────────────────

    private IEnumerable<EmojiCategory> FilteredCategories =>
        string.IsNullOrWhiteSpace(_search)
            ? AllCategories
            : AllCategories
                .Select(c => new EmojiCategory(
                    c.Name,
                    c.Emojis.Where(e => e.Name.Contains(_search, StringComparison.OrdinalIgnoreCase)).ToArray()))
                .Where(c => c.Emojis.Length > 0);

    // ── Emoji data ────────────────────────────────────────────────────────────

    private static readonly IReadOnlyList<EmojiCategory> AllCategories = BuildEmojiCategories();

    private static EmojiCategory[] BuildEmojiCategories() =>
    [
        new("Smileys", [ new("😀","grinning"), new("😂","joy"), new("😍","heart eyes"),
            new("😎","sunglasses"), new("🤔","thinking"), new("😴","sleeping"),
            new("😱","scream"), new("🥳","partying"), new("🤩","star struck"),
            new("😭","loudly crying"), new("🙂","slightly smiling"), new("😊","blush"),
            new("🤯","exploding head"), new("🥺","pleading"), new("😇","innocent"), new("🤗","hugging") ]),
        new("Gestures", [ new("👍","thumbs up"), new("👎","thumbs down"), new("🙌","raising hands"),
            new("👏","clapping"), new("🤝","handshake"), new("✌️","victory"),
            new("💪","muscle"), new("🖐️","hand"), new("👋","wave"),
            new("🤞","crossed fingers"), new("🤌","pinched fingers"), new("🫶","heart hands") ]),
        new("Hearts", [ new("❤️","red heart"), new("🧡","orange heart"), new("💛","yellow heart"),
            new("💚","green heart"), new("💙","blue heart"), new("💜","purple heart"),
            new("🖤","black heart"), new("🤍","white heart"), new("💔","broken heart"),
            new("💕","two hearts"), new("💗","growing heart"), new("💝","heart ribbon") ]),
        new("Stars & Fire", [ new("⭐","star"), new("🌟","glowing star"), new("✨","sparkles"),
            new("💫","dizzy"), new("🔥","fire"), new("💯","hundred"),
            new("🎯","bullseye"), new("⚡","lightning"), new("💥","collision"), new("🌈","rainbow") ]),
        new("Events", [ new("🎉","party popper"), new("🎊","confetti ball"), new("🎈","balloon"),
            new("🎁","gift"), new("🏆","trophy"), new("🥇","gold medal"),
            new("🎖️","medal"), new("🎗️","reminder ribbon"), new("🎀","ribbon"), new("🎪","circus tent") ]),
        new("Nature", [ new("🌍","earth"), new("🌱","seedling"), new("🌿","herb"),
            new("🌸","cherry blossom"), new("🌺","hibiscus"), new("🌻","sunflower"),
            new("⛅","partly cloudy"), new("🌊","wave"), new("🦋","butterfly"),
            new("🍁","maple leaf"), new("🌴","palm tree"), new("🌙","moon") ]),
        new("Animals", [ new("🐶","dog"), new("🐱","cat"), new("🦊","fox"),
            new("🐻","bear"), new("🐼","panda"), new("🦁","lion"),
            new("🐯","tiger"), new("🐮","cow"), new("🐷","pig"),
            new("🐸","frog"), new("🦄","unicorn"), new("🐉","dragon") ]),
        new("Food", [ new("🍕","pizza"), new("🍔","burger"), new("🎂","cake"),
            new("🍦","ice cream"), new("🍎","apple"), new("🍊","tangerine"),
            new("🍋","lemon"), new("🍇","grapes"), new("🍓","strawberry"),
            new("☕","coffee"), new("🥑","avocado"), new("🍜","noodles") ]),
        new("Objects", [ new("💡","bulb"), new("🔍","search"), new("🔑","key"),
            new("🔒","lock"), new("⚙️","gear"), new("🛠️","tools"),
            new("📱","phone"), new("💻","laptop"), new("🎮","game controller"),
            new("🔬","microscope"), new("🔭","telescope"), new("🧪","test tube") ]),
        new("Documents", [ new("📝","memo"), new("📄","document"), new("📋","clipboard"),
            new("📊","chart"), new("📈","chart up"), new("📉","chart down"),
            new("📌","pushpin"), new("📚","books"), new("📖","open book"),
            new("✏️","pencil"), new("💼","briefcase"), new("🗂️","folder") ]),
        new("Travel", [ new("🚀","rocket"), new("✈️","airplane"), new("🚂","train"),
            new("🚗","car"), new("🏠","house"), new("🏢","office"),
            new("🏛️","building"), new("🗺️","world map"), new("🧭","compass"),
            new("🏖️","beach"), new("⛵","sailboat"), new("🌆","city") ]),
    ];

    // ── Records ───────────────────────────────────────────────────────────────

    private sealed record EmojiCategory(string Name, EmojiItem[] Emojis);
    private sealed record EmojiItem(string Char, string Name);
}
