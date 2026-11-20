using System.Reflection;
using System.Text.Json;

namespace PoeFilterX.Common
{
    public static class GithubHelper
    {
        public static async Task<GithubApiReleasesResponse?> PullData(string author, string repo, string version)
        {
            var appName = Assembly.GetExecutingAssembly().GetName();
            var appVersion = SystemHelper.GetAssemblyVersionStr();
            var userAgentHeader = new System.Net.Http.Headers.ProductInfoHeaderValue(appName.Name ?? "PoeFilterX", appVersion);

            var tag = version == "latest" ? version : $"tags/{version}";

            var uri = $"https://api.github.com/repos/{author}/{repo}/releases/{tag}";

            using var client = new HttpClient();
            var msg = new HttpRequestMessage(HttpMethod.Get, uri);
            msg.Headers.UserAgent.Add(userAgentHeader);
            var resp = await client.SendAsync(msg);
            if (!resp.IsSuccessStatusCode)
            {
                await Console.Error.WriteLineAsync($"Something went wrong trying to access the following API uri, please check your version:\n\t'{uri}'");
                return null;
            }

            Console.WriteLine("Pulling Github API release info...");
            var contentStream = await resp.Content.ReadAsStreamAsync();
            var result = await JsonSerializer.DeserializeAsync<GithubApiReleasesResponse>(contentStream);

            if (result == null)
            {
                await Console.Error.WriteLineAsync($"Something went wrong trying to access the following API uri, please check your version:\n\t'{uri}'");
                return null;
            }

            Console.WriteLine($"Info retrieved! Release:'{result.name}' Author:'{result.author?.login}'");

            return result;
        }

        public static async Task DownloadFile(string uri, string downloadPath)
        {
            var appName = Assembly.GetExecutingAssembly().GetName();
            var appVersion = SystemHelper.GetAssemblyVersionStr();
            var userAgentHeader = new System.Net.Http.Headers.ProductInfoHeaderValue(appName.Name ?? "PoeFilterX", appVersion);

            using var client = new HttpClient();

            var downloadMsg = new HttpRequestMessage(HttpMethod.Get, uri);
            downloadMsg.Headers.UserAgent.Add(userAgentHeader);
            var downloadResp = await client.SendAsync(downloadMsg);

            await using var fs = new FileStream(downloadPath, FileMode.Create, FileAccess.Write);

            await downloadResp.Content.CopyToAsync(fs);
            await fs.FlushAsync();
        }
    }
}