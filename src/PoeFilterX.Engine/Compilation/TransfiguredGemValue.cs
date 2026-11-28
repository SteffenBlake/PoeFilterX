using System.Collections.Immutable;

namespace PoeFilterX.Engine.Compilation;

public sealed record TransfiguredGemValue(
    bool? Any,
    ImmutableArray<OperatorValue<ImmutableArray<string>>> Names
);
