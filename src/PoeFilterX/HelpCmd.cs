using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PoeFilterX
{
    internal static class HelpCmd
    {
        static string HelpText =
@"Utility for compiling .filterx projects into Path of Exile .filter files
==Commands==
    PoeFilterX build - Builds a .filterx project into a filter file.
    PoeFilterX version - Prints the version info of PoeFilterX.";

        internal static Task Run(string[] args)
        {
            var HelpList = new Dictionary<string, string>
            {
                { "", HelpCmd.HelpText },
                { "build", BuildCmd.HelpText },
                { "version", VersionCmd.HelpText },
            };

            var helpKey = string.Join(' ', args);
            Console.WriteLine(HelpList[helpKey]);
            return Task.CompletedTask;
        }
    }
}
