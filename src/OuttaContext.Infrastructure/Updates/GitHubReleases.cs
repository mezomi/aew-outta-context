using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OuttaContext.Core.Common;

namespace OuttaContext.Infrastructure.Updates;

/// <summary>Reads releases and downloads their files.</summary>
/// <remarks>A typed HttpClient: <see cref="Configure"/> sets it up when it is registered (AddHttpClient).</remarks>
public sealed class GitHubReleases(HttpClient client)
{
    /// <summary>Set to test updates against another server.</summary>
    public const string UrlOverrideVariable = "OUTTA_UPDATE_URL";

    public static void Configure(HttpClient client)
    {
        client.Timeout = TimeSpan.FromMinutes(10);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("outta-updater");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public async Task<Result<Release, Error>> LatestAsync(string repository, CancellationToken cancellationToken)
    {
        string url = Environment.GetEnvironmentVariable(UrlOverrideVariable) ?? $"https://api.github.com/repos/{repository}/releases/latest";
        try
        {
            using HttpResponseMessage response = await client.GetAsync(new Uri(url), cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return UpdateErrors.NoRelease();
            }

            if (!response.IsSuccessStatusCode)
            {
                return UpdateErrors.HttpStatus((int)response.StatusCode);
            }

            Release? release = await response.Content.ReadFromJsonAsync<Release>(cancellationToken);
            return release is null ? UpdateErrors.EmptyAnswer() : release;
        }
        catch (HttpRequestException exception)
        {
            return UpdateErrors.Unreachable(exception.Message);
        }
        catch (JsonException exception)
        {
            return UpdateErrors.Unreachable(exception.Message);
        }
    }

    /// <summary>Downloads a file to <paramref name="destination"/>, reporting bytes done.</summary>
    public async Task<UnitResult<Error>> DownloadAsync(
        string url, string destination, Action<double, double?> onProgress, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await client.GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return UpdateErrors.HttpStatus((int)response.StatusCode);
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

            return UnitResult.Success<Error>();
        }
        catch (HttpRequestException exception)
        {
            File.Delete(destination);
            return UpdateErrors.DownloadFailed(exception.Message);
        }
    }
}
