using System.Collections.Frozen;
using System.Collections.Immutable;

namespace PoeFilterX.Engine.Checking;

internal sealed class ItemBaseCatalog
{
    private static readonly Lazy<ItemBaseCatalog> Poe1 = new(() => new(GameVersion.Poe1));
    private static readonly Lazy<ItemBaseCatalog> Poe2 = new(() => new(GameVersion.Poe2));
    private readonly FrozenDictionary<string, ImmutableArray<ItemBase>> byClass;
    private readonly FrozenDictionary<string, ImmutableArray<ItemBase>> byName;

    private ItemBaseCatalog(GameVersion game)
    {
        var items = game == GameVersion.Poe1 ? GameData.Poe1ItemBases() : GameData.Poe2ItemBases();
        byClass = items.GroupBy(item => item.ItemClass, StringComparer.Ordinal)
            .ToFrozenDictionary(group => group.Key, group => group.OrderByDescending(item => item.Name.Length).ToImmutableArray());
        byName = items.GroupBy(item => item.Name, StringComparer.Ordinal)
            .ToFrozenDictionary(group => group.Key, group => group.ToImmutableArray());
    }

    internal static ItemBaseCatalog For(GameVersion game) => game == GameVersion.Poe1 ? Poe1.Value : Poe2.Value;

    internal ItemBase? Find(
        string label,
        string itemClass,
        bool allowAffixes = false
    )
    {
        if (byName.TryGetValue(label, out var exact))
        {
            return exact.FirstOrDefault(item => item.ItemClass == itemClass);
        }

        // Magic item labels include affixes. Restrict longest whole-name matching to the selected game's item class.
        if (!allowAffixes || !byClass.TryGetValue(itemClass, out var candidates))
        {
            return null;
        }

        return candidates.FirstOrDefault(item => ContainsBase(label, item.Name));
    }

    private static bool ContainsBase(string label, string name)
    {
        var start = label.IndexOf(name, StringComparison.Ordinal);

        return start >= 0 && (start == 0 || label[start - 1] == ' ')
            && (start + name.Length == label.Length || label[start + name.Length] == ' ');
    }

}
