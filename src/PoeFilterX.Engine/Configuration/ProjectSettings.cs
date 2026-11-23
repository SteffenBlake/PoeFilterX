using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Checking;
using PoeFilterX.Engine.Packages;
using PoeFilterX.Engine.Projects;
using PoeFilterX.Engine.Results;

namespace PoeFilterX.Engine.Configuration;

public sealed record ProjectSettings(
    string Root,
    string? Profile,
    IConfiguration Configuration,
    IReadOnlyDictionary<string, string?>? CommandLine = null
)
{
    public const string FileName = ".poefilterx.json";
    public const string SchemaUrl = "https://raw.githubusercontent.com/SteffenBlake/PoeFilterX/main/schemas/poefilterx.schema.json";

    public static string ProfileFileName(string profile) => $".poefilterx.{profile}.json";

    public string? EntryPoint => ResolvePath(Configuration["entrypoint"]);

    public string? Output => ResolvePath(Configuration["output"]);

    public string CacheRoot => ResolvePath(Configuration["packageCache"]) ?? PackageIdentity.CacheRoot;

    public ImmutableArray<string> Files => Profile is null
        ? [Path.Combine(Root, FileName)]
        : [Path.Combine(Root, FileName), Path.Combine(Root, ProfileFileName(Profile))];

    public Dictionary<string, string> PackageOverrides => SectionValues("packageOverrides")
        .ToDictionary(pair => pair.Key, pair => ResolvePath(pair.Value)!, StringComparer.Ordinal);

    public Dictionary<string, string> Variables => SectionValues("variables");

    public string? ResolvePath(string? path) => path is null ? null : Path.GetFullPath(path, Root);

    public T Get<T>(string key, T fallback = default!) => Configuration.GetValue(key, fallback)!;

    public string[] GetArray(string key) => Configuration.GetSection(key).Get<string[]>() ?? [];

    private Dictionary<string, string> SectionValues(string section)
    {
        var prefix = section + ":";
        return Configuration.AsEnumerable().Where(pair => pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && pair.Value is not null)
            .ToDictionary(
                pair => pair.Key[prefix.Length..], pair => pair.Value!, StringComparer.OrdinalIgnoreCase
            );
    }

    public static bool IsConfigurationFile(string path)
    {
        var name = Path.GetFileName(path);
        return name.Equals(FileName, StringComparison.Ordinal)
            || name.StartsWith(".poefilterx.", StringComparison.Ordinal)
            && name.EndsWith(".json", StringComparison.Ordinal) && name.Length > ".poefilterx..json".Length;
    }

    public static Result<ProjectSettings> LoadForInput(
        string? input,
        string? profile = null,
        IReadOnlyDictionary<string, string?>? commandLine = null
    )
    {
        if (input is not null && IsConfigurationFile(input))
        {
            if (!File.Exists(input))
            {
                return Error($"Configuration file '{input}' was not found.", Path.GetDirectoryName(Path.GetFullPath(input))!);
            }

            var name = Path.GetFileName(input);
            if (name != FileName)
            {
                profile ??= name[".poefilterx.".Length..^".json".Length];
            }
        }

        var directory = input is null ? Directory.GetCurrentDirectory() : Path.GetDirectoryName(Path.GetFullPath(input))!;
        return Load(directory, profile, commandLine);
    }

    public static string FindRoot(string directory)
    {
        string? fallback = null;
        for (var current = Path.GetFullPath(directory); current is not null; current = Path.GetDirectoryName(current))
        {
            if (File.Exists(Path.Combine(current, FileName)))
            {
                return current;
            }

            if (fallback is null && !EntrypointDiscovery.Find(current).IsEmpty)
            {
                fallback = current;
            }
        }

        return fallback ?? Path.GetFullPath(directory);
    }

    public static Result<ProjectSettings> Load(
        string directory,
        string? profile = null,
        IReadOnlyDictionary<string, string?>? commandLine = null
    )
    {
        var root = Path.GetFullPath(directory);
        try
        {
            root = FindRoot(directory);
            profile ??= Environment.GetEnvironmentVariable("POEFILTERX_PROFILE");
            if (profile is not null && (profile.Length == 0 || profile.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_'))))
            {
                return Error("Profile names may contain only letters, digits, hyphens, and underscores.", root);
            }

            var baseFile = Path.Combine(root, FileName);
            var profileFile = profile is null ? null : Path.Combine(root, ProfileFileName(profile));
            if (profileFile is not null && !File.Exists(profileFile))
            {
                return Error($"Profile configuration '{profileFile}' was not found.", root);
            }

            // PhysicalFileProvider excludes dotfiles by default. Streams also avoid retaining file watchers.
            using var baseStream = File.Exists(baseFile) ? File.OpenRead(baseFile) : null;
            using var profileStream = profileFile is not null ? File.OpenRead(profileFile) : null;
            var builder = new ConfigurationBuilder();
            if (baseStream is not null)
            {
                builder.AddJsonStream(baseStream);
            }

            if (profileStream is not null)
            {
                builder.AddJsonStream(profileStream);
            }

            builder.AddEnvironmentVariables("POEFILTERX_");
            builder.AddInMemoryCollection(EnvironmentAliases());
            if (commandLine is not null)
            {
                builder.AddInMemoryCollection(commandLine);
            }

            var settings = new ProjectSettings(root, profile, builder.Build(), commandLine);
            return settings.Validate();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or FormatException or InvalidDataException or ArgumentException or InvalidOperationException or JsonException)
        {
            return Error($"Cannot load project configuration: {exception.Message}", root);
        }
    }

    public static void AddPackageOverrides(IDictionary<string, string?> values, string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("Package overrides must be a JSON object mapping repository URLs to directories.");
        }

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw new FormatException("Package override directories must be strings.");
            }

            values["packageOverrides:" + property.Name] = property.Value.GetString();
        }
    }

    private Result<ProjectSettings> Validate()
    {
        foreach (var key in new[] { "noRestore", "locked", "update", "stdio" })
        {
            _ = Get(key, false);
        }

        foreach (var key in new[] { "areaLevel", "socket" })
        {
            _ = Get<int?>(key);
        }

        if (!Enum.TryParse<LogLevel>(Configuration["verbosity"] ?? "Information", true, out var level)
            || !Enum.IsDefined(level))
        {
            return Error("Verbosity must be a defined log level.", Root);
        }

        if (Get<int?>("socket") is < 1 or > 65535)
        {
            return Error("Socket port must be between 1 and 65535.", Root);
        }

        if (!Enum.TryParse<GameVersion>(Configuration["game"] ?? "Poe1", true, out var game)
            || !Enum.IsDefined(game))
        {
            return Error("Game must be Poe1 or Poe2.", Root);
        }

        if (Configuration["format"] is { } format
            && !new[] { "Text", "Json" }.Contains(format, StringComparer.OrdinalIgnoreCase))
        {
            return Error("Format must be Text or Json.", Root);
        }

        foreach (var key in new[] { "entrypoint", "output", "packageCache", "itemFile", "pipe" })
        {
            if (Configuration[key] is { Length: 0 })
            {
                return Error($"Configuration '{key}' cannot be empty.", Root);
            }
        }

        foreach (var key in new[] { "packageOverrides", "variables", "fact", "define" })
        {
            if (Configuration[key] is not null)
            {
                return Error($"Configuration '{key}' must be an object or array, not a scalar.", Root);
            }
        }

        foreach (var pair in SectionValues("packageOverrides"))
        {
            if (!PackageIdentity.IsRepository(pair.Key) || string.IsNullOrWhiteSpace(pair.Value)
                || !Directory.Exists(ResolvePath(pair.Value)))
            {
                return Error($"Invalid package override '{pair.Key}': directory '{pair.Value}' must exist.", Root);
            }
        }

        return this;
    }

    private static Dictionary<string, string?> EnvironmentAliases()
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in new Dictionary<string, string>
        {
            ["NO_RESTORE"] = "noRestore",
            ["PACKAGE_CACHE"] = "packageCache",
            ["ITEM_FILE"] = "itemFile",
            ["AREA_LEVEL"] = "areaLevel",
            ["PROJECT_NAME"] = "name"
        })
        {
            if (Environment.GetEnvironmentVariable("POEFILTERX_" + pair.Key) is { } value)
            {
                values[pair.Value] = value;
            }
        }

        if (Environment.GetEnvironmentVariable("POEFILTERX_PACKAGE_OVERRIDES") is { } json)
        {
            AddPackageOverrides(values, json);
        }

        return values;
    }

    private static ParseError Error(string message, string root)
    {
        return new ParseError(DiagnosticCodes.BuildFailure, message).At(Path.Combine(root, FileName), default);
    }
}
