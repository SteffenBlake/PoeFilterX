using PoeFilterX.Engine.Checking;

namespace PoeFilterX.Engine.Tests.Checking;

[Parallelizable(ParallelScope.All)]
public class ItemCompletionDataTests
{
    [TestCase("Conquest Lamellar", "Body Armours Str Dex T1")]
    [TestCase("Marshall's Brigandine", "Body Armours Str Dex T2")]
    [TestCase("Full Wyvernscale", "Body Armours Str Dex T3")]
    [TestCase("Vermillion Ring", "Rings T1")]
    [TestCase("Steel Ring", "Rings T1")]
    [TestCase("Diamond Ring", "Rings T2")]
    [TestCase("Ruby Ring", "Rings T3")]
    [TestCase("Chaos Orb", "Stackable Currency")]
    public void Poe1_names_expose_search_tags(string name, string tags)
    {
        Assert.That(
            ItemCompletionData.Poe1BaseTypes,
            Does.Contain(new BaseTypeSuggestion(name, tags, "poe1"))
        );
    }

    [Test]
    public void Game_tags_merge_shared_entries_and_preserve_game_specific_classifications()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ItemCompletionData.BaseTypes.Single(item => item.Name == "Chaos Orb").SearchTags,
                Is.EqualTo("Stackable Currency poe1 poe2"));
            Assert.That(ItemCompletionData.BaseTypes.Single(item => item.Name == "Conquest Lamellar").Games,
                Is.EqualTo("poe1"));
            Assert.That(ItemCompletionData.BaseTypes.Single(item => item.Name == "Rawhide Belt").Games,
                Is.EqualTo("poe2"));
            Assert.That(ItemCompletionData.ItemClassSuggestions.Single(item => item.Name == "Rings").Games,
                Is.EqualTo("poe1 poe2"));
            Assert.That(ItemCompletionData.ItemClassSuggestions.Single(item => item.Name == "Rune Daggers").Games,
                Is.EqualTo("poe1"));
        }
    }

    [TestCase("Biostatic Ring", "Rings T1")]
    [TestCase("Prismatic Ring", "Rings T2")]
    [TestCase("Ruby Ring", "Rings T3")]
    [TestCase("Corona Amulet", "Amulets T1")]
    [TestCase("Stellar Amulet", "Amulets T2")]
    [TestCase("Lunar Amulet", "Amulets T3")]
    [TestCase("Fine Belt", "Belts T1")]
    [TestCase("Mail Belt", "Belts T1")]
    [TestCase("Long Belt", "Belts T2")]
    [TestCase("Linen Belt", "Belts T3")]
    [TestCase("Rawhide Belt", "Belts T3")]
    public void Poe2_jewellery_uses_three_drop_level_bands(string name, string tags)
    {
        Assert.That(
            ItemCompletionData.Poe2BaseTypes,
            Does.Contain(new BaseTypeSuggestion(name, tags, "poe2"))
        );
    }

    [Test]
    public void All_poe2_jewellery_tiers_are_banded()
    {
        var jewellery = ItemCompletionData.Poe2BaseTypes.Where(item =>
            item.Tags.StartsWith("Rings ", StringComparison.Ordinal)
            || item.Tags.StartsWith("Amulets ", StringComparison.Ordinal)
            || item.Tags.StartsWith("Belts ", StringComparison.Ordinal));
        Assert.That(jewellery.All(item => item.Tags.Split(' ')[^1] is "T1" or "T2" or "T3"), Is.True);
    }

    [Test]
    public void Lists_are_sorted_unique_and_contain_display_names()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ItemCompletionData.Poe1ItemClasses, Does.Contain("Rune Daggers").And.Not.Contain("Rune Dagger"));
            Assert.That(ItemCompletionData.Poe2ItemClasses, Does.Contain("Quarterstaves"));
            Assert.That(ItemCompletionData.Poe2BaseTypes.Any(item =>
                item.Name == "Stocky Mitts" && item.Tags.StartsWith("Gloves Str T", StringComparison.Ordinal)), Is.True);
            Assert.That(ItemCompletionData.ItemClasses, Is.Ordered.Using<string>(StringComparer.Ordinal).And.Unique);
            Assert.That(ItemCompletionData.ItemClasses.All(name => !string.IsNullOrWhiteSpace(name)), Is.True);
            Assert.That(ItemCompletionData.BaseTypes, Is.Unique);
            Assert.That(ItemCompletionData.BaseTypes.Select(item => item.Name), Is.Ordered.Using<string>(StringComparer.Ordinal));
            Assert.That(ItemCompletionData.Poe1BaseTypes.Where(item => item.Name == "Diamond Ring")
                .Select(item => item.Tags), Is.EqualTo((string[])["Rings T2"]));
            Assert.That(ItemCompletionData.BaseTypes.All(item => !string.IsNullOrWhiteSpace(item.Tags)), Is.True);
        }
    }
}
