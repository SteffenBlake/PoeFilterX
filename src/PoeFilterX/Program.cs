using System.CommandLine;

namespace PoeFilterX;

internal static class Program
{
    private static int Main(string[] args)
    {
        var root = new RootCommand("Compile .filterx projects into Path of Exile .filter files.");
        BuildCmd.Configure(root);
        root.Subcommands.Add(BuildCmd.Create());

        return root.Parse(args).Invoke();
    }
}
