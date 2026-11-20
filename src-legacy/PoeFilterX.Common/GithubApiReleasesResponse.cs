
// ReSharper disable UnusedMember.Global
#pragma warning disable IDE1006 // Naming Styles
namespace PoeFilterX.Common
{

    public class GithubApiReleasesResponse
    {
        public string? url { get; set; }
        public string? assets_url { get; set; }
        public string? upload_url { get; set; }
        public string? html_url { get; set; }
        public int? id { get; set; }
        public GithubApiAuthor? author { get; set; }
        public string? node_id { get; set; }
        public string? tag_name { get; set; }
        public string? target_commitish { get; set; }
        public string? name { get; set; }
        public bool? draft { get; set; }
        public bool? prerelease { get; set; }
        public DateTime? created_at { get; set; }
        public DateTime? published_at { get; set; }
        public GithubApiAsset[]? assets { get; set; }
        public string? tarball_url { get; set; }
        public string? zipball_url { get; set; }
        public string? body { get; set; }
    }

    public class GithubApiAuthor
    {
        public string? login { get; set; }
        public int? id { get; set; }
        public string? node_id { get; set; }
        public string? avatar_url { get; set; }
        public string? gravatar_id { get; set; }
        public string? url { get; set; }
        public string? html_url { get; set; }
        public string? followers_url { get; set; }
        public string? following_url { get; set; }
        public string? gists_url { get; set; }
        public string? starred_url { get; set; }
        public string? subscriptions_url { get; set; }
        public string? organizations_url { get; set; }
        public string? repos_url { get; set; }
        public string? events_url { get; set; }
        public string? received_events_url { get; set; }
        public string? type { get; set; }
        public bool? site_admin { get; set; }
    }

    public class GithubApiAsset
    {
        public string? url { get; set; }
        public int? id { get; set; }
        public string? node_id { get; set; }
        public string? name { get; set; }
        public object? label { get; set; }
        public GithubApiAuthor? uploader { get; set; }
        public string? content_type { get; set; }
        public string? state { get; set; }
        public int? size { get; set; }
        public int? download_count { get; set; }
        public DateTime? created_at { get; set; }
        public DateTime? updated_at { get; set; }
        public string? browser_download_url { get; set; }
    }
}
#pragma warning restore IDE1006 // Naming Styles