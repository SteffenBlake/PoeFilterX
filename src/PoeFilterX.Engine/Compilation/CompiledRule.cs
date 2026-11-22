using System.Collections.Immutable;

namespace PoeFilterX.Engine.Compilation;

public sealed record CompiledRule(
    PreparedBlock Block,
    StyleOperations Style,
    ImmutableArray<PreparedStyle> Declarations
);
