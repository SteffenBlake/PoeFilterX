using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Results;

namespace PoeFilterX.Engine.Packages;

public static class PackagePaths
{
    public static bool IsWithin(string path, string root)
    {
        var relative = Path.GetRelativePath(root, path);
        return !Path.IsPathRooted(relative) && relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    public static bool HasLink(string path, string root)
    {
        if (!IsWithin(path, root))
        {
            return true;
        }

        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }

            if (Path.GetRelativePath(root, current) == ".")
            {
                return false;
            }
        }

        return true;
    }

    public static Result<string?> Resolve(
        string value,
        string from,
        PackageCatalog catalog
    )
    {
        try
        {
            if (value.StartsWith('@'))
            {
                var separator = value.IndexOf('/');
                var alias = separator < 0 ? value[1..] : value[1..separator];
                var package = catalog.References.FirstOrDefault(reference => reference.Alias == alias);
                if (package is null)
                {
                    return new ParseError(
                        DiagnosticCodes.PackagePath,
                        $"Package '@{alias}' has not been imported by the entrypoint."
                    );
                }

                if (separator < 0 || separator == value.Length - 1)
                {
                    return new ParseError(
                        DiagnosticCodes.PackagePath,
                        "A package reference must include a file path: @alias/path/file.fss."
                    );
                }

                return package.Root is null ? (string?)null : ResolveWithin(value[(separator + 1)..], package.Root, package.Root);
            }

            var owner = catalog.References.FirstOrDefault(reference => reference.Root is { } root && IsWithin(from, root));
            var directory = Path.GetDirectoryName(from)!;
            return owner?.Root is { } boundary
                ? ResolveWithin(value, directory, boundary)
                : Path.GetFullPath(value, directory);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return new ParseError(DiagnosticCodes.PackagePath, exception.Message);
        }
    }

    private static Result<string?> ResolveWithin(
        string value,
        string directory,
        string boundary
    )
    {
        var path = Path.GetFullPath(value, directory);
        if (!IsWithin(path, boundary) || HasLink(path, boundary))
        {
            return new ParseError(
                DiagnosticCodes.PackagePath,
                "Package file paths must stay inside the package and cannot follow symbolic links."
            );
        }

        return path;
    }
}
