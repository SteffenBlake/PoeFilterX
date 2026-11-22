using System.Collections.Immutable;

namespace PoeFilterX.Engine.Checking;

internal sealed record DefenceRange(int Minimum, int Maximum);

internal sealed record ItemBase(
    string Name,
    string ItemClass,
    int Width,
    int Height,
    int DropLevel,
    string Domain,
    bool HasImplicit,
    ImmutableArray<string> Tags,
    DefenceRange? Armour,
    DefenceRange? Evasion,
    DefenceRange? EnergyShield,
    DefenceRange? Ward
);
