using System.Net.Http.Json;
using System.Text.Json.Serialization;
using OuttaContext.Core.Results;

namespace OuttaContext.Infrastructure.Updates;

/// <summary>A file attached to a GitHub release.</summary>
public sealed record ReleaseAsset(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("browser_download_url")] string DownloadUrl,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("digest")] string? Digest);

/// <summary>The parts of GitHub's "latest release" answer we use.</summary>
public sealed record Release(
    [property: JsonPropertyName("tag_name")] string Tag,
    [property: JsonPropertyName("assets")] IReadOnlyList<ReleaseAsset> Assets);

/// <summary>Reads releases and downloads their files.</summary>
public sealed class GitHubReleases(HttpClient client)
{
    public const string Tool = "GitHub";

    public static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("outta-updater");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public async Task<ToolResult<Release>> LatestAsync(string repository, CancellationToken cancellationToken)
    {
        string url = Environment.GetEnvironmentVariable("OUTTA_UPDATE_URL") ?? $"https://api.github.com/repos/{repository}/releases/latest";
        try
        {
            using HttpResponseMessage response = await client.GetAsync(new Uri(url), cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new ToolError(Tool, "No release has been published yet.", "Try again later.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return new ToolError(Tool, $"GitHub answered with HTTP {(int)response.StatusCode}.", "Try again in a few minutes.");
            }

            Release? release = await response.Content.ReadFromJsonAsync<Release>(cancellationToken);
            return release is null
                ? new ToolError(Tool, "GitHub's answer was empty.", "Try again in a few minutes.")
                : ToolResult<Release>.Success(release);
        }
        catch (HttpRequestException exception)
        {
            return new ToolError(Tool, $"Could not reach GitHub: {exception.Message}", "Check your connection.");
        }
    }

    /// <summary>Downloads a file to <paramref name="destination"/>, reporting bytes done.</summary>
    public async Task<ToolResult<Done>> DownloadAsync(
        string url, string destination, Action<double, double?> onProgress, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await client.GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new ToolError(Tool, $"Download failed with HTTP {(int)response.StatusCode}.", "Try again in a few minutes.");
            }

            double? total = response.Content.Headers.ContentLength;
            await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using FileStream target = File.Create(destination);
            var buffer = new byte[256 * 1024];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                done += read;
                onProgress(done, total);
            }

            return ToolResult.Success();
        }
        catch (HttpRequestException exception)
        {
            File.Delete(destination);
            return new ToolError(Tool, $"Download failed: {exception.Message}", "Check your connection.");
        }
    }
}
