using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            var executingPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? 
                throw new ApplicationException("Something has gone very wrong with this process, what are you doing?");

            var info = FileVersionInfo.GetVersionInfo(executingPath);

            if (string.IsNullOrEmpty(info.ProductName))
            {
                var execAssembly = Assembly.GetCallingAssembly();
                var name = execAssembly.GetName();
                Console.WriteLine("No ProductName found, you are probably doing this on Linux right?");
                Console.WriteLine("If reporting a bug be sure to also include the output of 'uname -r'");
                Console.WriteLine($"{name.Name}-{name.Version}");
            } 
            else
            {
                Console.WriteLine($"{info.ProductName}-{info.ProductVersion}");
            }


            return Task.CompletedTask;
        }


    }
}
