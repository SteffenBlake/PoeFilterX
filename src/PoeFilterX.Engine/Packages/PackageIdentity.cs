using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PoeFilterX.Engine.Packages;

public static partial class PackageIdentity
{
    public static bool IsAlias(string value) => AliasPattern().IsMatch(value);

    public static bool IsCommit(string value)
    {
        return value.Length is 40 or 64 && value.All(char.IsAsciiHexDigit);
    }

    public static bool IsRepository(string value)
    {
        if (value.Any(char.IsControl) || value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        if (value.Contains("://", StringComparison.Ordinal))
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && uri.Scheme is "https" or "http" or "ssh"
                && uri.Host.Length != 0 && uri.AbsolutePath.Length > 1
                && uri.Fragment.Length == 0 && uri.Query.Length == 0
                && (uri.Scheme == "ssh" ? !uri.UserInfo.Contains(':') : uri.UserInfo.Length == 0);
        }

        return ScpPattern().IsMatch(value);
    }

    public static string CacheRoot => Environment.GetEnvironmentVariable("POEFILTERX_PACKAGE_CACHE")
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify),
            "poefilterx",
            "packages"
        );

    public static string RepositoryDirectory(string cacheRoot, string repository)
    {
        var key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(repository)));
        return Path.Combine(cacheRoot, key);
    }

    public static string SnapshotDirectory(
        string cacheRoot,
        string repository,
        string commit
    )
    {
        return Path.Combine(RepositoryDirectory(cacheRoot, repository), commit.ToLowerInvariant());
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex AliasPattern();

    [GeneratedRegex(@"^(?:[A-Za-z0-9_.-]+@)?[A-Za-z0-9_][A-Za-z0-9_.-]*:[^\s:]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ScpPattern();
}
