using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PoeFilterX
{
    public static class UpdateCmd
    {
        internal static string HelpText =
@"Updates PoeFilterX.
    Usage: PoeFilterX update [--v|--version=<version>]
    [--v|--version=<version>] - Specific release ver to update/downgrade to. Defaults to 'latest'
    [--p|--platform=<platform>] - Executable Platform to pull. Defaults to win-x64
    [--a|--author=<name>] - Github Repo Owner to pull from. Defaults to 'SteffenBlake (Me!)'
    [--r|--repo=<name> - Github Repo Name to pull from. Defaults to 'PoeFilterX'
";
        internal static async Task Run(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables("POEFILTERX_")
                .AddCommandLine(args)
                .Build();

            var version = config["v"] ?? config["version"];

            if (version != null)
            {
                version = "tag/" + version;
            }
            else
            {
                version = "latest";
            }

            var author = config["a"] ?? config["repo"] ?? "SteffenBlake";
            var repo = config["r"] ?? config["author"] ?? "PoeFilterX";
            var platform = config["p"] ?? config["platform"];

            var uri = $"https://api.github.com/repos/{author}/{repo}/releases/{version}";
            var appName = Assembly.GetCallingAssembly().GetName();
            var userAgentHeader = new System.Net.Http.Headers.ProductInfoHeaderValue(appName.Name, appName.Version.ToString());
            
            using var client = new HttpClient();
            var msg = new HttpRequestMessage(HttpMethod.Get, uri);
            msg.Headers.UserAgent.Add(userAgentHeader);
            var resp = await client.SendAsync(msg);
            if (!resp.IsSuccessStatusCode)
            {
                Console.Error.WriteLine("We were unable to access the GitHub API for some reason. Try again later.");
                return;
            }

            Console.WriteLine("Pulling Github API release info...");
            var contentStream = await resp.Content.ReadAsStreamAsync();
            var result = await JsonSerializer.DeserializeAsync<GithubApiReleasesResponse>(contentStream);

            if (result == null)
            {
                Console.Error.WriteLine($"Something went wrong trying to access the following API uri, please check your version:\n\t'{uri}'");
                return;
            }

            Console.WriteLine($"Info retrieved! Release:'{result.name}' Author:'{result.author.login}'");

            var executingPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            var executingFolder = Path.GetDirectoryName(executingPath);
            if (executingFolder == null)
            {
                Console.Error.WriteLine("Unable to discern executing folder. Something went wrong.");
                return;
            }

            var actualTargetVersion = result.tag_name;

            if (platform == null)
            {
                platform =
                System.OperatingSystem.IsWindows() ? "win-" :
                System.OperatingSystem.IsMacOS() ? "osx-" :
                System.OperatingSystem.IsLinux() ? "linux-" :
                null;

                if (platform == null)
                {
                    Console.Error.WriteLine("Unable to auto-detect platform. Please specify a supported platform via the '--p/--platform' argument");
                    return;
                }

                platform += System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLower();
            }

            var target = $"{actualTargetVersion}-{platform}";

            Console.WriteLine($"Targeting version -> {target}");

            // We now have our target file
            var targetFileName = target + ".zip";
            var targetFile = result.assets.SingleOrDefault(a => a.name == targetFileName);

            if (targetFile == null)
            {
                Console.Error.WriteLine($"Unable to locate file on this release. Please double check the file list here:\n\t {result.html_url}");
                return;
            }

            var downloadPath = Path.Combine(executingFolder, targetFileName);

            if (File.Exists(downloadPath))
            {
                Console.WriteLine("Old download still exists, cleaning up...");
                File.Delete(downloadPath);
            }

            Console.WriteLine($"Downloading file...\n\tFrom:'{targetFile.browser_download_url}'\n\tTo:'{downloadPath}'");

            var downloadMsg = new HttpRequestMessage(HttpMethod.Get, targetFile.browser_download_url);
            downloadMsg.Headers.UserAgent.Add(userAgentHeader);
            var downloadResp = await client.SendAsync(downloadMsg);

            using (var fs = new FileStream(downloadPath, FileMode.Create, FileAccess.Write))
            {
                await downloadResp.Content.CopyToAsync(fs);
                await fs.FlushAsync();
            }

            var extractDir = Path.Combine(executingFolder, "temp");
            Console.WriteLine($"Zip file downloaded. Unzipping to {extractDir}");

            if (Directory.Exists(extractDir))
            {
                Console.WriteLine("Old extract dir still exists. Cleaning up...");
                Directory.Delete(extractDir, true);
            }

            ZipFile.ExtractToDirectory(downloadPath, extractDir);
            File.Delete(downloadPath);

            Console.WriteLine("Commencing bootstrap process. Updating the Updater");
            var updaterExePath = Path.Combine(extractDir, "PoeFilterX.Update.exe");
            var updaterLinuxPath = Path.Combine(extractDir, "PoeFilterX.Update");

            var updaterPath = File.Exists(updaterExePath) ? updaterExePath : File.Exists(updaterLinuxPath) ? updaterLinuxPath : null;
            if (updaterPath == null)
            {
                Console.Error.WriteLine($"Unable to locate Updater files, expected at either:\n\t.exe: {updaterExePath}\n\tlinux: {updaterLinuxPath}");
                return;
            }

            var updaterFileName = Path.GetFileName(updaterPath);
            var updaterCopyToPath = Path.Combine(executingFolder, updaterFileName);

            File.Copy(updaterExePath, updaterCopyToPath, true);

            Console.WriteLine("Bootstrapping complete! Updater is synched. Updating PoeFilterX now...");

            Process.Start(updaterFileName);
        }
    }
}
