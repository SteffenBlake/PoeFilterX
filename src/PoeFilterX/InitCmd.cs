using System.Text.Json;
using System.Text.RegularExpressions;

namespace PoeFilterX
{
    internal static class InitCmd
    {
        internal static string HelpText =
@"Through a series of prompts, initializes a poefilterx template NPM project ready to use in the current directory
    Usage: poefilterx init
";
        internal static Task Run(string[] args)
        {
            Console.WriteLine();
            Console.Write("Project Name: ");
            var projectName = Console.ReadLine();

            var projectNameKebab = ToKebabCase(projectName);
            if (string.IsNullOrEmpty(projectNameKebab))
            {
                Console.Error.WriteLine($"Invalid project name");
                Environment.Exit(1);
            }

            var currentDir = Environment.CurrentDirectory;
            var projectDir = Path.Combine(currentDir, projectNameKebab);

            if (Directory.Exists(projectDir))
            {
                Console.Error.WriteLine($"Directory '{projectDir}' already exists, cannot overwrite");
                Environment.Exit(1);
            }

            _ = Directory.CreateDirectory(projectDir);

            var packagePath = Path.Combine(projectDir, "package.json");

            Console.WriteLine($"Creating '{packagePath}'");

            var packageData = new
            {
                name = projectNameKebab,
                version = "1.0.0",
                description = "",
                main = "main.poefilterx",
                author = new
                {
                    name = ""
                },
                scripts = new
                {
                    build = "poefilterx build"
                }
            };
            File.WriteAllText(packagePath, JsonSerializer.Serialize(packageData, new JsonSerializerOptions { WriteIndented = true }));

            var filterXPath = Path.Combine(projectDir, "main.poefilterx");
            Console.WriteLine($"Creating '{filterXPath}'");
            File.WriteAllText(filterXPath, "using main.fss");

            var stylePath = Path.Combine(projectDir, "main.fss");
            Console.WriteLine($"Creating '{stylePath}'");
            _ = File.Create(stylePath);

            Console.WriteLine();

            Console.WriteLine("Project created, you can test it out by executing `npm run build` inside the project now!");

            return Task.CompletedTask;
        }

        public static string? ToKebabCase(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            // Replace all non-alphanumeric characters with a dash
            value = Regex.Replace(value, @"[^0-9a-zA-Z]", "-");

            // Replace all subsequent dashes with a single dash
            value = Regex.Replace(value, @"[-]{2,}", "-");

            // Remove any trailing dashes
            value = Regex.Replace(value, @"-+$", string.Empty);

            // Remove any dashes in position zero
            if (value.StartsWith("-"))
            {
                value = value[1..];
            }

            // Lowercase and return
            return value.ToLower();
        }
    }
}
