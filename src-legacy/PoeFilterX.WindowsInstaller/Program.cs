using Microsoft.Extensions.Configuration;
using PoeFilterX.Common;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Principal;

namespace PoeFilterX.WindowsInstaller
{
    internal class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables("POEFILTERX_INSTALLER_")
                .AddCommandLine(args)
                .Build();

            var result = 0;
            if (!OperatingSystem.IsWindows())
            {
                await Console.Error.WriteLineAsync("Update functionality only supported for Windows at this time");
                result = 1;
            }
            else
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                if (principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    if (!await RunInstall(config))
                    {
                        // error occurred
                        result = 1;
                    }
                }
                else
                {
                    await Console.Error.WriteLineAsync("Update functionality must be run as Administrator to work.");
                    result = 1;
                }
            }

            if (config["y"] == null)
            {
                Console.WriteLine("<Press enter to finish>");
                _ = Console.ReadLine();
            }
            
            // Exit code 1 if an error occurred
            return result;
        }

        private static async Task<bool> RunInstall(IConfigurationRoot config)
        {
            var version = SystemHelper.GetAssemblyVersionStr();
            if (version == null)
            {
                return false;
            }

            var platform = SystemHelper.GetSystemPlatform();

            var target = $"{version}-{platform}";

            var author = config["a"] ?? config["author"] ?? "SteffenBlake";
            var repo = config["r"] ?? config["repo"] ?? "PoeFilterX";

            var installFolder = config["p"] ?? config["path"] ?? "C:\\Program Files\\PoeFilterX\\";
            if (!Directory.Exists(installFolder))
            {
                _ = Directory.CreateDirectory(installFolder);
            }

            var executingPath = Process.GetCurrentProcess().MainModule?.FileName;
            var executingFolder = Path.GetDirectoryName(executingPath);
            if (executingFolder == null)
            {
                await Console.Error.WriteLineAsync("Unable to discern executing folder. Something went wrong.");
                return false;
            }

            var repoData = await GithubHelper.PullData(author, repo, version);
            if (repoData == null)
            {
                return false;
            }

            var targetFileName = $"PoeFilterX-{target}.zip";

            var targetFile = repoData.assets?.SingleOrDefault(a => a.name == targetFileName)?.browser_download_url;
            if (targetFile == null)
            {
                await Console.Error.WriteLineAsync($"Could not locate associated zip file {targetFileName}");
                return false;
            }

            var downloadPath = Path.Combine(installFolder, targetFileName);
            await GithubHelper.DownloadFile(targetFile, downloadPath);

            var processes = Process.GetProcesses().Where(p => p.ProcessName is "PoeFilterX" or "PoeFilterX.exe");

            while (processes.Any())
            {
                Console.WriteLine("Waiting for PoeFilterX to terminate...");
                await Task.Delay(1000);
                processes = Process.GetProcesses().Where(p => p.ProcessName is "PoeFilterX" or "PoeFilterX.exe");
            }

            Console.WriteLine($"Zip file downloaded. Unzipping to {installFolder}");

            ZipFile.ExtractToDirectory(downloadPath, installFolder, true);
            File.Delete(downloadPath);

            const EnvironmentVariableTarget scope = EnvironmentVariableTarget.Machine;
            var path = Environment.GetEnvironmentVariable("PATH", scope) ?? "";
            if (!path.Contains(installFolder))
            {
                path += $";{installFolder}";
            }

            Environment.SetEnvironmentVariable("PATH", path, scope);

            Console.WriteLine(
                "Installation complete! Please run 'PoeFilterX version' to verify! (May require a system restart)");
            return true;
        }
    }
}