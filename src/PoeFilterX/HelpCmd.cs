namespace PoeFilterX
{
    internal static class HelpCmd
    {
        private const string HelpText = @"Utility for compiling .filterx projects into Path of Exile .filter files
Type poefilterx help <command> for more details for a given command.
==Commands==
    poefilterx build - Builds a .filterx project into a filter file.
    poefilterx version - Prints the version info of PoeFilterX.
    poefilterx update - Updates PoeFilterX";

        internal static Task Run(string[] args)
        {
            var HelpList = new Dictionary<string, string>
            {
                { "", HelpCmd.HelpText },
                { "build", BuildCmd.HelpText },
                { "version", VersionCmd.HelpText },
                { "update", UpdateCmd.HelpText },
            };

            var helpKey = string.Join(' ', args);
            Console.WriteLine(HelpList[helpKey]);
            return Task.CompletedTask;
        }
    }
}
