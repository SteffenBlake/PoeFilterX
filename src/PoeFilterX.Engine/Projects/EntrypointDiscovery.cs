using System.Collections.Immutable;

namespace PoeFilterX.Engine.Projects;

public static class EntrypointDiscovery
{
    public const string DefaultFileName = "entrypoint.filterx";

    public static ImmutableArray<string> Find(string directory, IEnumerable<string>? additionalPaths = null)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var files = Directory.Exists(directory) ? Directory.EnumerateFiles(directory) : [];

        return [.. files.Concat(additionalPaths ?? [])
            .Where(path => comparer.Equals(Path.GetDirectoryName(path), directory) && IsEntrypoint(path))
            .Distinct(comparer)
            .Order(comparer)];
    }

    private static bool IsEntrypoint(string path)
    {
        var name = Path.GetFileName(path);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        return name.Equals(DefaultFileName, comparison)
            || (name.Length > DefaultFileName.Length + 1 && name.EndsWith("." + DefaultFileName, comparison));
    }
}
