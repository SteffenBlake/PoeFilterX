using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PoeFilterX
{
    internal static class VersionCmd
    {
        internal static string HelpText =
@"Prints the version info of PoeFilterX
    Usage: PoeFilterX version";

        internal static Task Run(string[] args)
        {
            var execAssembly = Assembly.GetCallingAssembly();
            var name = execAssembly.GetName();

            Console.WriteLine($"{name.Name}-{name.Version}");

            return Task.CompletedTask;
        }


    }
}
