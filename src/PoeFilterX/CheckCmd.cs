using System.CommandLine;
using System.Buffers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoeFilterX.Engine.Checking;
using PoeFilterX.Engine.Configuration;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Handlers;
using PoeFilterX.Engine.Projects;
using PoeFilterX.Engine.Results;

namespace PoeFilterX;

internal static class CheckCmd
{
    private enum OutputFormat { Text, Json }

    private sealed record Invocation(
        string? Path,
        string? Item,
        string? ItemFile,
        GameVersion Game,
        int? AreaLevel,
        OutputFormat Format,
        string[] Facts,
        string[] Defines,
        bool NoRestore,
        bool Locked,
        LogLevel Verbosity,
        ProjectSettings Settings
    );

    internal static Command Create()
    {
        var command = new Command("check", "Inspect which filter block and styles apply to a copied game item.");
        var path = new Argument<string?>("path") { Arity = ArgumentArity.ZeroOrOne };
        var item = new Option<string?>("--item") { Description = "Full item text copied in game. Omit to paste at the prompt." };
        var file = new Option<string?>("--item-file") { Description = "Read copied item text from a UTF-8 file." };
        var game = new Option<GameVersion>("--game") { Description = "Poe1 or Poe2.", DefaultValueFactory = _ => GameVersion.Poe1 };
        var area = new Option<int?>("--area-level") { Description = "Drop zone level; defaults to T16 (PoE 1: 83, PoE 2: 80)." };
        var format = new Option<OutputFormat>("--format") { Description = "Text (default) or Json." };
        var facts = new Option<string[]>("--fact") { Description = "Override a missing fact: --fact BaseArmour=100. Separate text alternatives with |." };
        var defines = new Option<string[]>("--define") { Description = "Set a compiler variable: --define name=value." };
        var noRestore = new Option<bool>("--no-restore");
        var locked = new Option<bool>("--locked");
        var verbosity = CommandLogging.CreateVerbosityOption();
        CommandConfiguration.AddOptions(command);
        command.Arguments.Add(path);
        foreach (var option in new Option[] { item, file, game, area, format, facts, defines, noRestore, locked, verbosity })
        {
            command.Options.Add(option);
        }

        command.Validators.Add(result =>
        {
            if (result.GetValue(item) is not null && result.GetValue(file) is not null)
            {
                result.AddError("Supply --item or --item-file, not both.");
            }
        });
        command.SetAction((result, token) => CommandConfiguration.InvokeAsync(result, result.GetValue(path), settings => HandleAsync(new(
            settings.EntryPoint,
            settings.Configuration["item"],
            settings.ResolvePath(settings.Configuration["itemFile"]),
            settings.Get("game", GameVersion.Poe1),
            settings.Get<int?>("areaLevel"),
            settings.Get("format", OutputFormat.Text),
            settings.GetArray("fact"),
            settings.GetArray("define"),
            settings.Get("noRestore", false),
            settings.Get("locked", false),
            settings.Get("verbosity", LogLevel.Information),
            settings
        ), token)));

        return command;
    }

    private static async Task<int> HandleAsync(Invocation request, CancellationToken token)
    {
        using var logging = CommandLogging.CreateFactory(request.Verbosity, standardErrorOnly: true);
        var logger = logging.CreateLogger("Check");
        ProjectSnapshot? snapshot = null;
        try
        {
            if (request.Item is not null && request.ItemFile is not null)
            {
                return Report(new ParseError(DiagnosticCodes.InvalidItem, "Supply --item or --item-file, not both."), request.Format, snapshot);
            }

            var entrypoint = EntrypointResolver.Resolve(request.Path, request.Settings);
            if (entrypoint is ParseError entrypointError)
            {
                return Report(entrypointError, request.Format, snapshot);
            }

            snapshot = await CompilerProjectLoader.LoadAsync(
                (string)entrypoint.Value!,
                new(Locked: request.Locked, NoRestore: request.NoRestore, Settings: request.Settings),
                logger,
                token
            );
            if (!snapshot.Diagnostics.IsEmpty)
            {
                return Report(new ParseError(snapshot.Diagnostics), request.Format, snapshot);
            }

            var overrides = Assignments(request.Facts);
            var variables = Assignments(request.Defines);
            if (overrides is ParseError overridesError)
            {
                return Report(overridesError, request.Format, snapshot);
            }

            if (variables is ParseError variablesError)
            {
                return Report(variablesError, request.Format, snapshot);
            }

            Result<string> text = request.Item is { } supplied ? supplied
                : request.ItemFile is { } input ? await ReadFileAsync(input, token) : await ReadItemAsync(token);
            if (text is ParseError itemError)
            {
                return Report(itemError, request.Format, snapshot);
            }

            var checkedItem = ItemChecker.Check(snapshot, (string)text.Value!, request.Game, request.AreaLevel,
                ProjectConfiguration.Read((Dictionary<string, string>)variables.Value!, request.Settings),
                (Dictionary<string, string>)overrides.Value!, token);
            return Report(checkedItem, request.Format, snapshot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Report(new ParseError(DiagnosticCodes.InvalidItem, exception.Message), request.Format, snapshot);
        }
    }

    private static Result<Dictionary<string, string>> Assignments(string[] values)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            var index = value.IndexOf('=');
            if (index < 1)
            {
                return new ParseError(DiagnosticCodes.InvalidItem, $"Expected name=value, got '{value}'.");
            }

            result[value[..index]] = value[(index + 1)..];
        }

        return result;
    }

    private static async Task<Result<string>> ReadFileAsync(string path, CancellationToken token)
    {
        try
        {
            using var reader = new StreamReader(path);

            return await ReadBoundedAsync(reader, token);
        }
        catch (ArgumentException exception)
        {
            return new ParseError(DiagnosticCodes.InvalidItem, exception.Message);
        }
    }

    private static async Task<Result<string>> ReadItemAsync(CancellationToken token)
    {
        await Console.Error.WriteLineAsync("Hover the item in Path of Exile and press Ctrl+C, then paste the item here.", token);
        if (Console.IsInputRedirected)
        {
            return await ReadBoundedAsync(Console.In, token);
        }

        await Console.Error.WriteLineAsync("After pasting, press Enter on an empty line to check (or enter a single . on a new line).", token);
        var text = new StringBuilder(2048);
        while (await Console.In.ReadLineAsync(token) is { } line)
        {
            if (line == "." || (line.Length == 0 && text.Length > 0))
            {
                break;
            }

            if (text.Length + line.Length + Environment.NewLine.Length > CopiedItemParser.MaximumTextLength)
            {
                return new ParseError(DiagnosticCodes.InvalidItem, "Copied item text exceeds 1 MiB.");
            }

            text.AppendLine(line);
        }

        return text.ToString();
    }

    private static async Task<Result<string>> ReadBoundedAsync(TextReader reader, CancellationToken token)
    {
        var buffer = ArrayPool<char>.Shared.Rent(4096);
        try
        {
            var text = new StringBuilder(2048);
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory(0, 4096), token)) != 0)
            {
                if (text.Length + read > CopiedItemParser.MaximumTextLength)
                {
                    return new ParseError(DiagnosticCodes.InvalidItem, "Copied item text exceeds 1 MiB.");
                }

                text.Append(buffer.AsSpan(0, read));
            }

            return text.ToString();
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    private static int Report(
        Result<CheckResult> result,
        OutputFormat format,
        ProjectSnapshot? snapshot
    )
    {
        var error = result.Value as ParseError;
        var value = result.Value as CheckResult;
        if (format == OutputFormat.Json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(new CheckResponse(1, value, error?.Diagnostics ?? []),
                CheckJsonContext.Default.CheckResponse));
        }
        else if (value is not null)
        {
            CheckRenderer.Write(value, Console.Out);
        }

        if (error is not null)
        {
            if (snapshot is not null)
            {
                DiagnosticRenderer.Write(snapshot, error.Diagnostics, Console.Error);
            }
            else
            {
                foreach (var diagnostic in error.Diagnostics)
                {
                    Console.Error.WriteLine($"error {diagnostic.Code}: {diagnostic.Message}");
                }
            }
        }

        return error is null ? 0 : 1;
    }
}
