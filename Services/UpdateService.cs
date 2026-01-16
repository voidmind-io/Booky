using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Booky.Services;

public class UpdateService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/voidmind-io/Booky/releases/latest";
    private readonly HttpClient _httpClient;

    public UpdateService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Booky-UpdateChecker");
    }

    public string CurrentVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.0.0";
        }
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<GitHubRelease>(GitHubApiUrl);

            if (response == null || string.IsNullOrEmpty(response.TagName))
                return null;

            // Parse version from tag (e.g., "v2.1.0" -> "2.1.0")
            var latestVersion = response.TagName.TrimStart('v');

            if (IsNewerVersion(latestVersion, CurrentVersion))
            {
                // Find the installer asset
                var installerAsset = response.Assets?.FirstOrDefault(a =>
                    a.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true);

                return new UpdateInfo
                {
                    CurrentVersion = CurrentVersion,
                    LatestVersion = latestVersion,
                    ReleaseUrl = response.HtmlUrl ?? $"https://github.com/voidmind-io/Booky/releases/tag/{response.TagName}",
                    DownloadUrl = installerAsset?.BrowserDownloadUrl,
                    ReleaseNotes = response.Body
                };
            }

            return null;
        }
        catch
        {
            // Silently fail - update check is not critical
            return null;
        }
    }

    private static bool IsNewerVersion(string latest, string current)
    {
        try
        {
            var latestParts = latest.Split('.').Select(int.Parse).ToArray();
            var currentParts = current.Split('.').Select(int.Parse).ToArray();

            for (int i = 0; i < Math.Min(latestParts.Length, currentParts.Length); i++)
            {
                if (latestParts[i] > currentParts[i]) return true;
                if (latestParts[i] < currentParts[i]) return false;
            }

            return latestParts.Length > currentParts.Length;
        }
        catch
        {
            return false;
        }
    }

    private class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    private class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}

public class UpdateInfo
{
    public string CurrentVersion { get; set; } = "";
    public string LatestVersion { get; set; } = "";
    public string ReleaseUrl { get; set; } = "";
    public string? DownloadUrl { get; set; }
    public string? ReleaseNotes { get; set; }
}
