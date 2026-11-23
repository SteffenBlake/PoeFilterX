using System.Collections;
using PoeFilterX.Engine.Configuration;

namespace PoeFilterX.Engine.Projects;

public static class ProjectConfiguration
{
    public static Dictionary<string, string> Read(
        IReadOnlyDictionary<string, string>? commandLine,
        ProjectSettings? settings = null
    )
    {
        var values = settings?.Variables ?? [with(StringComparer.OrdinalIgnoreCase)];
        foreach (DictionaryEntry item in Environment.GetEnvironmentVariables())
        {
            var key = (string)item.Key;
            if (key.StartsWith("POEFILTERX_", StringComparison.OrdinalIgnoreCase)
                && !Reserved(key[11..]) && !key.Contains("__", StringComparison.Ordinal))
            {
                values[key[11..]] = (string)item.Value!;
            }
        }

        if (commandLine is not null)
        {
            foreach (var item in commandLine)
            {
                values[item.Key] = item.Value;
            }
        }

        return values;
    }

    private static bool Reserved(string key)
    {
        return new[] { "ENTRYPOINT", "OUTPUT", "VERBOSITY", "PROFILE", "PACKAGE_CACHE", "PACKAGECACHE",
            "PACKAGE_OVERRIDES", "NO_RESTORE", "NORESTORE", "LOCKED", "UPDATE", "ITEM", "ITEM_FILE", "ITEMFILE",
            "GAME", "AREA_LEVEL", "AREALEVEL", "FORMAT", "NAME", "PATH", "STDIO", "SOCKET", "PIPE", "PROJECT_NAME" }
            .Contains(key, StringComparer.OrdinalIgnoreCase);
    }
}
