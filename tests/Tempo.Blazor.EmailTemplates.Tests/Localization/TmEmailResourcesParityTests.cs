using System.Collections;
using System.Globalization;
using System.Resources;
using Tempo.Blazor.EmailTemplates.Resources;

namespace Tempo.Blazor.EmailTemplates.Tests.Localization;

public class TmEmailResourcesParityTests
{
    private static readonly ResourceManager Manager =
        new(typeof(TmEmailResources).FullName!, typeof(TmEmailResources).Assembly);

    private static HashSet<string> KeysFor(CultureInfo culture)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var set = Manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        if (set is not null)
            foreach (DictionaryEntry entry in set)
                keys.Add((string)entry.Key);
        return keys;
    }

    [Fact]
    public void Neutral_HasKeys()
    {
        KeysFor(CultureInfo.InvariantCulture).Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("fr")]
    public void EveryNeutralKey_IsTranslated(string culture)
    {
        var neutral = KeysFor(CultureInfo.InvariantCulture);
        var translated = KeysFor(new CultureInfo(culture));

        neutral.Except(translated, StringComparer.Ordinal).Should().BeEmpty(
            $"every neutral resource key must have a {culture} translation");
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("fr")]
    public void NoExtraKeys_InTranslations(string culture)
    {
        var neutral = KeysFor(CultureInfo.InvariantCulture);
        var translated = KeysFor(new CultureInfo(culture));

        translated.Except(neutral, StringComparer.Ordinal).Should().BeEmpty(
            $"the {culture} resource must not define keys missing from the neutral resource");
    }
}
