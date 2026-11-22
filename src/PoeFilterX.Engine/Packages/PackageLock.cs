using System.Collections.Immutable;

namespace PoeFilterX.Engine.Packages;

internal sealed record PackageLock(int Version, ImmutableArray<PackageLock.Entry> Packages)
{
    public sealed record Entry(string Alias, string Repository, string? RequestedCommit, string Commit);
}
