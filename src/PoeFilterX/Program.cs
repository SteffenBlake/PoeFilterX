using Microsoft.Extensions.Configuration;
using PoeFilterX.Business.Models;
using System.Reflection;

namespace PoeFilterX
{
    internal class Program
    {
        private static Dictionary<string, Func<string[], Task>> Commands = new()
        {
            { "--help", HelpCmd.Run },
            { "help", HelpCmd.Run },
            { "build", BuildCmd.Run },
            { "-v", VersionCmd.Run },
            { "--version", VersionCmd.Run },
            { "version", VersionCmd.Run },
            { "update", UpdateCmd.Run }
        };

        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Command required, type 'PoeFilterX help' for a list of commands");
                Environment.Exit(1);
            }

            var command = args[0].ToLower().Trim();

            if (!Commands.ContainsKey(command))
            {
                Console.Error.WriteLine($"Unrecognized Command '{args[0]}'");
                Environment.Exit(1);
            }

            args = args.Skip(1).ToArray();

            await Commands[command](args);
        }
    }
}