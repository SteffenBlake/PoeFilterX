using Microsoft.Extensions.Configuration;
using PoeFilterX.Common;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;

namespace PoeFilterX
{
    public static class UpdateCmd
    {
        internal static string HelpText =
@"Updates PoeFilterX, defaults to fetching latest version but can specify one to set to.
Also supports pulling forks of PoeFilterX from alternative authors/repos
Take extreme caution when updating from someone else's repo. 
Just because the executable is named PoeFilterX does NOT necessarily mean it's a legit version of the program. 
    Usage: poefilterx update
    [--v|--version ""version""] - Specific release ver to update/downgrade to. Defaults to 'latest'
    [--p|--platform ""platform""] - Executable Platform to pull. Defaults to win-x64
    !!! [--a|--author ""name""] - Github Repo Owner to pull from. Defaults to 'SteffenBlake (Me!)'
    !!! [--r|--repo ""name""] - Github Repo Name to pull from. Defaults to 'PoeFilterX'
";
        internal static async Task Run(string[] args)
        {
            if (OperatingSystem.IsWindows())
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    await Console.Error.WriteLineAsync("Update functionality must be run as Administrator to work.");
                    return;
                }
            } 
            else
            {
                await Console.Error.WriteLineAsync("Update functionality only supported for Windows at this time");
                return;
            }

            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables("POEFILTERX_")
                .AddCommandLine(args)
                .Build();

            var version = config["v"] ?? config["version"] ?? "latest";

            var author = config["a"] ?? config["author"] ?? "SteffenBlake";
            var repo = config["r"] ?? config["repo"] ?? "PoeFilterX";
            var platform = config["p"] ?? config["platform"];

            var result = await GithubHelper.PullData(author, repo, version);
            if (result == null)
            {
                return;
            }

            var appName = Assembly.GetExecutingAssembly().GetName();
            var currentVersionRaw = appName.Version;
            if (currentVersionRaw == null)
            {
                await Console.Error.WriteLineAsync("Unable to discern executing version. Something went wrong.");
                return;
            }

            var currentVersionStr = $"{currentVersionRaw.Major}.{currentVersionRaw.Minor}.{currentVersionRaw.Build}";

            var actualTargetVersion = result.tag_name;
            Console.WriteLine($"Current version is {currentVersionStr}");
            if (actualTargetVersion == currentVersionStr)
            {
                Console.WriteLine("Already up to date!");
                return;
            }

            var executingPath = Process.GetCurrentProcess().MainModule?.FileName;
            var executingFolder = Path.GetDirectoryName(executingPath);
            if (executingFolder == null)
            {
                await Console.Error.WriteLineAsync("Unable to discern executing folder. Something went wrong.");
                return;
            }

            if (platform == null)
            {
                platform =
                OperatingSystem.IsWindows() ? "win-" :
                OperatingSystem.IsMacOS() ? "osx-" :
                OperatingSystem.IsLinux() ? "linux-" :
                null;

                if (platform == null)
                {
                    await Console.Error.WriteLineAsync("Unable to auto-detect platform. Please specify a supported platform via the '--p/--platform' argument");
                    return;
                }

                platform += System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLower();
            }

            var target = $"{actualTargetVersion}-{platform}";

            Console.WriteLine($"Targeting version -> {target}");

            // We now have our target file
            var targetFileName = 
                OperatingSystem.IsWindows() ? $"PoeFilterX.WindowsInstaller-{actualTargetVersion}.exe" 
                : null;
            
            if (targetFileName == null)
            {
                await Console.Error.WriteLineAsync("Update functionality only supported for Windows at this time");
                return;
            }

            var targetFile = result.assets?.SingleOrDefault(a => a.name == targetFileName)?.browser_download_url;

            if (targetFile == null)
            {
                await Console.Error.WriteLineAsync($"Unable to locate file on this release. Please double check the file list here:\n\t {result.html_url}\n\tFileName:{targetFileName}");
                return;
            }

            var downloadPath = Path.Combine(executingFolder, targetFileName);

            if (File.Exists(downloadPath))
            {
                Console.WriteLine("Old download still exists, cleaning up...");
                File.Delete(downloadPath);
            }

            Console.WriteLine($"Downloading file...\n\tFrom:'{targetFile}'\n\tTo:'{downloadPath}'");
            await GithubHelper.DownloadFile(targetFile, downloadPath);

            Console.WriteLine("Bootstrapping complete! Installer is synched. Updating PoeFilterX now...\n\n\n\n\n");
            _ = Process.Start(downloadPath, $"--a=\"{author}\" --r=\"{repo}\"");

        }
    }
}
