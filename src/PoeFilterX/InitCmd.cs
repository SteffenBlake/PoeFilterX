using System.CommandLine;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PoeFilterX.Engine.Projects;

namespace PoeFilterX;

internal static partial class InitCmd
{
    internal static Command Create()
    {
        var command = new Command("init", "Create a .filterx project and stylesheet.");
        var name = new Option<string?>("--name", "--project-name")
        {
            Description = "Project name. Omit to enter it at a prompt.",
            Arity = ArgumentArity.ExactlyOne
        };
        var path = new Option<string?>("--path")
        {
            Description = "Parent directory for the new project. Defaults to the current directory.",
            Arity = ArgumentArity.ExactlyOne
        };
        var verbosity = CommandLogging.CreateVerbosityOption();
        command.Options.Add(name);
        command.Options.Add(path);
        command.Options.Add(verbosity);
        command.SetAction(result =>
        {
            using var logging = CommandLogging.CreateFactory(result.GetValue(verbosity));

            return Run(
                result.GetValue(name),
                result.GetValue(path),
                logging.CreateLogger(nameof(InitCmd))
            );
        });

        return command;
    }

    private static int Run(
        string? name,
        string? parent,
        ILogger logger
    )
    {
        if (name is null)
        {
            Console.Write("Project Name: ");
            name = Console.ReadLine();
        }

        var normalized = NameSeparators().Replace(name ?? string.Empty, "-").Trim('-').ToLowerInvariant();
        if (normalized.Length == 0)
        {
            InitializationFailed(logger, "Invalid project name");
            return 1;
        }

        try
        {
            var directory = Path.GetFullPath(Path.Combine(
                parent ?? Directory.GetCurrentDirectory(),
                normalized
            ));
            if (Path.Exists(directory))
            {
                InitializationFailed(
                    logger,
                    $"Directory '{directory}' already exists, cannot overwrite"
                );
                return 1;
            }

            Directory.CreateDirectory(directory);
            WriteFile(
                Path.Combine(directory, EntrypointDiscovery.DefaultFileName),
                $$"""
                using {{normalized}}.fss;

                # Highlight items not covered by a rule.
                Style missed;

                # Show all currency with the base style.
                {
                    Class Currency;
                    Style base;
                }

                """,
                logger
            );
            WriteFile(
                Path.Combine(directory, normalized + ".fss"),
                """
                /* Loud styling makes unmatched items easy to spot. */
                missed {
                    show: true;
                    bg-color: 0xFF1493;
                    font-color: 0xFFFF00;
                    border-color: 0x00FFFF;
                    font-size: 45;
                    icon: disabled;
                    pillar: disabled;
                }

                /* A simple starting style for items you want to show. */
                base {
                    show: true;
                    bg-color: 0x302015;
                    border-color: 0x000000;
                    font-color: 0xFFFFFF;
                }

                /* Hide items without the diagnostic colors. */
                hidden {
                    show: false;
                    bg-color: 0x00000000;
                    border-color: 0x000000;
                    icon: disabled;
                    pillar: disabled;
                }

                """,
                logger
            );
            ProjectCreated(logger, directory);

            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            InitializationFailed(logger, exception.Message);
            return 1;
        }
    }

    private static void WriteFile(
        string path,
        string contents,
        ILogger logger
    )
    {
        CreatingFile(logger, path);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream);
        writer.Write(contents);
    }

    [GeneratedRegex("[^0-9a-zA-Z]+", RegexOptions.CultureInvariant)]
    private static partial Regex NameSeparators();

    [LoggerMessage(
        2000,
        LogLevel.Information,
        "Creating '{Path}'"
    )]
    private static partial void CreatingFile(ILogger logger, string path);

    [LoggerMessage(
        2001,
        LogLevel.Information,
        "Project created in '{Path}'. Run `poefilterx` inside that directory to build it."
    )]
    private static partial void ProjectCreated(ILogger logger, string path);

    [LoggerMessage(
        2002,
        LogLevel.Error,
        "PFX4001: {Message}"
    )]
    private static partial void InitializationFailed(ILogger logger, string message);
}
