using Microsoft.Extensions.Configuration;
using PoeFilterX.Common;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Principal;

namespace PoeFilterX.WindowsInstaller
{
    internal class Program
    {
        public static async Task Main(string[] args)
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

            var version = SystemHelper.GetAssemblyVersionStr();
            if (version == null)
                return;

            var platform = SystemHelper.GetSystemPlatform();

            var target = $"{version}-{platform}";

            var author = config["a"] ?? config["author"];
            if (author == null)
            {
                await Console.Error.WriteLineAsync("Github Author Parameter is required");
                return;
            }
            var repo = config["r"] ?? config["repo"];
            if (repo == null)
            {
                await Console.Error.WriteLineAsync("Github Repo Parameter is required");
                return;
            }

            var installFolder = config["p"] ?? config["path"] ?? "C:\\Program Files\\PoeFilterX\\";
            if (!Directory.Exists(installFolder))
            {
                Directory.CreateDirectory(installFolder);
            }

            var executingPath = Process.GetCurrentProcess().MainModule?.FileName;
            var executingFolder = Path.GetDirectoryName(executingPath);
            if (executingFolder == null)
            {
                await Console.Error.WriteLineAsync("Unable to discern executing folder. Something went wrong.");
                return;
            }

            var repoData = await GithubHelper.PullData(author, repo, version);
            if (repoData == null)
                return;

            var targetFileName = $"PoeFilterX-{target}.zip";

            var targetFile = repoData.assets?.SingleOrDefault(a => a.name == targetFileName)?.browser_download_url;
            if (targetFile == null)
            {
                await Console.Error.WriteLineAsync($"Could not locate associated zip file {targetFileName}");
                return;
            }

            var downloadPath = Path.Combine(installFolder, targetFileName);
            await GithubHelper.DownloadFile(targetFile, downloadPath);

            var processes = Process.GetProcesses().Where(p => p.ProcessName == "PoeFilterX" || p.ProcessName == "PoeFilterX.exe");

            while (processes.Any())
            {
                Console.WriteLine("Waiting for PoeFilterX to terminate...");
                await Task.Delay(1000);
                processes = Process.GetProcesses().Where(p => p.ProcessName == "PoeFilterX" || p.ProcessName == "PoeFilterX.exe");
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

            Console.WriteLine("Installation complete! Please run 'PoeFilterX version' to verify! (May require a system restart)");
        }
    }
}