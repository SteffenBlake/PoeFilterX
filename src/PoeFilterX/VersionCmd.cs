using System.Diagnostics;
using System.Reflection;

namespace PoeFilterX
{
    internal static class VersionCmd
    {
        internal static string HelpText =
@"Prints the version info of PoeFilterX
    Usage: poefilterx [version|-v]";

        internal static Task Run(string[] args)
        {
            var executingPath = Process.GetCurrentProcess().MainModule?.FileName ?? 
                throw new ApplicationException("Something has gone very wrong with this process, what are you doing?");

            var info = FileVersionInfo.GetVersionInfo(executingPath);

            if (string.IsNullOrEmpty(info.ProductName))
            {
                var execAssembly = Assembly.GetExecutingAssembly();
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
