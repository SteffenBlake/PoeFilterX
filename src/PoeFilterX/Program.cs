using System.CommandLine;

namespace PoeFilterX;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var root = new RootCommand("Compile .filterx projects into Path of Exile .filter files.");
        BuildCmd.Configure(root, variables);
        root.Subcommands.Add(BuildCmd.Create(variables));
        root.Subcommands.Add(InitCmd.Create());
        root.Subcommands.Add(RestoreCmd.Create());
        root.Subcommands.Add(CheckCmd.Create());
        root.Subcommands.Add(LanguageServerCmd.Create());
        var parsed = root.Parse(args);
        if (parsed.Errors.Count == 0 || parsed.CommandResult.Command.Name is "init" or "language-server" or "restore" or "check")
        {
            return await parsed.InvokeAsync();
        }

        var arguments = new List<string>(args.Length);
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (argument == "--")
            {
                arguments.AddRange(args.Skip(index));
                break;
            }

            var name = argument.Split('=', 2)[0];
            if (argument.StartsWith("--", StringComparison.Ordinal)
                && name is not ("--no-restore" or "--locked" or "--path" or "--p" or "--output" or "--o" or "--verbosity" or "--verbose" or "--v" or "--help" or "--version")
                && index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                variables[argument[2..]] = args[++index];
                continue;
            }

            arguments.Add(argument);
        }

        return await root.Parse([.. arguments]).InvokeAsync();
    }
}
