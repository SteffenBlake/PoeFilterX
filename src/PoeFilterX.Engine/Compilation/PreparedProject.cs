using System.Collections.Immutable;

namespace PoeFilterX.Engine.Compilation;

public sealed record PreparedProject(
    ImmutableArray<PreparedBlock> Blocks,
    ImmutableArray<PreparedStyle> Styles
);

