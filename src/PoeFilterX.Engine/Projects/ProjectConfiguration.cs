using System.Collections;

namespace PoeFilterX.Engine.Projects;

public static class ProjectConfiguration
{
    public static Dictionary<string, string> Read(IReadOnlyDictionary<string, string>? commandLine)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry item in Environment.GetEnvironmentVariables())
        {
            var key = (string)item.Key;
            if (key.StartsWith("POEFILTERX_", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("POEFILTERX_PACKAGE_CACHE", StringComparison.OrdinalIgnoreCase))
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
}
