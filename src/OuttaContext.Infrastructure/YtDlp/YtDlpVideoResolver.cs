using System.Text.Json;
using OuttaContext.Core.Media;
using OuttaContext.Core.Results;
using OuttaContext.Core.Sources;
using OuttaContext.Infrastructure.Processes;

namespace OuttaContext.Infrastructure.YtDlp;

/// <summary>
/// Asks yt-dlp what a link is (<c>yt-dlp -J</c>): title, length and the stream addresses of the
/// best formats up to the user's maximum quality. Nothing is downloaded.
/// </summary>
public sealed class YtDlpVideoResolver(IProcessRunner runner, ToolLocator tools, YtDlpOptions options) : IVideoResolver
{
    public async Task<ToolResult<ResolvedVideo>> ResolveAsync(
        RequestVariant request, int maxQuality, Action<string> detail, CancellationToken cancellationToken)
    {
        if (tools.Find("yt-dlp") is not { } ytDlp)
        {
            return YtDlpMissing();
        }

        var arguments = new List<string>(options.CommonArguments())
        {
            "--dump-single-json", "--no-playlist", "--format", FormatSelector(maxQuality),
        };
        foreach ((string name, string value) in request.Headers)
        {
            arguments.Add("--add-headers");
            arguments.Add($"{name}:{value}");
        }

        arguments.Add(ExtractorUrl(request.Url, request.ExtractorQuery));
        ProcessResult result = await runner.RunAsync(
            new ProcessSpec(ytDlp, arguments) { OnErrorLine = detail }, cancellationToken);

        if (!result.Succeeded)
        {
            return YtDlpErrors.Translate(result.Error);
        }

        try
        {
            return ParseInfo(result.Output, request.Url);
        }
        catch (JsonException exception)
        {
            return new ToolError(YtDlpErrors.Tool, $"yt-dlp returned unreadable output: {exception.Message}", "Run `outta update` to update yt-dlp.");
        }
    }

    /// <summary>Best video+audio up to <paramref name="maxQuality"/> lines; MP4/M4A preferred.</summary>
    public static string FormatSelector(int maxQuality) =>
        $"bv*[height<={maxQuality}][ext=mp4]+ba[ext=m4a]/bv*[height<={maxQuality}]+ba/b[height<={maxQuality}]/bv*+ba/b";

    /// <summary>
    /// Adds extra data for yt-dlp's site extractor to the URL ("smuggling", yt-dlp's own mechanism).
    /// Used to tell Dailymotion which page and player a video is embedded in.
    /// </summary>
    public static string ExtractorUrl(string url, IReadOnlyDictionary<string, string>? extractorQuery)
    {
        if (extractorQuery is null || extractorQuery.Count == 0)
        {
            return url;
        }

        string json = JsonSerializer.Serialize(new Dictionary<string, object> { ["query"] = extractorQuery });
        return $"{url}#__youtubedl_smuggle={Uri.EscapeDataString(json)}";
    }

    public static ToolError YtDlpMissing() =>
        new(YtDlpErrors.Tool, "yt-dlp was not found.", "Run `outta update` to download it, then try again.");

    private static ToolResult<ResolvedVideo> ParseInfo(string json, string url)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.TryGetProperty("_type", out JsonElement type) && type.GetString() == "playlist")
        {
            return new ToolError(YtDlpErrors.Tool, $"{url} is a playlist.", "Use the link of a single video.");
        }

        var info = new VideoInfo(
            Text(root, "id") ?? string.Empty,
            Text(root, "title") ?? "untitled",
            root.TryGetProperty("duration", out JsonElement duration) && duration.ValueKind == JsonValueKind.Number ? duration.GetDouble() : null,
            Text(root, "webpage_url") ?? url,
            Text(root, "extractor_key") ?? Text(root, "extractor") ?? string.Empty);

        IEnumerable<JsonElement> formats = root.TryGetProperty("requested_formats", out JsonElement requested)
            && requested.ValueKind == JsonValueKind.Array
                ? requested.EnumerateArray()
                : [root];
        var streams = new List<StreamFormat>();
        foreach (JsonElement format in formats)
        {
            if (Text(format, "url") is not { } streamUrl)
            {
                return new ToolError(YtDlpErrors.Tool, "yt-dlp found the video but no stream address for it.", "Run `outta update` to update yt-dlp.");
            }

            var headers = new Dictionary<string, string>();
            if (format.TryGetProperty("http_headers", out JsonElement headerObject) && headerObject.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty header in headerObject.EnumerateObject())
                {
                    headers[header.Name] = header.Value.ToString();
                }
            }

            streams.Add(new StreamFormat(streamUrl, Text(format, "protocol") ?? "https", headers));
        }

        return ToolResult<ResolvedVideo>.Success(new ResolvedVideo(info, streams));
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
