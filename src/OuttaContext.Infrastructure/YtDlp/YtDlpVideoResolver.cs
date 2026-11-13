using System.Text.Json;
using OuttaContext.Core.Common;
using OuttaContext.Core.Media;
using OuttaContext.Core.Reporting;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Sources;
using OuttaContext.Core.Time;
using OuttaContext.Infrastructure.Processes;

namespace OuttaContext.Infrastructure.YtDlp;

/// <summary>
/// Asks yt-dlp what a link is (<c>yt-dlp -J</c>): title, length and the stream addresses of the
/// best formats up to the user's maximum quality. Nothing is downloaded.
/// </summary>
public sealed class YtDlpVideoResolver(IProcessRunner runner, ToolLocator tools, YtDlpOptions options, IReporter reporter) : IVideoResolver
{
    public async Task<Result<ResolvedVideo, Error>> ResolveAsync(
        RequestVariant request, VideoQuality maxQuality, CancellationToken cancellationToken)
    {
        if (!tools.Find("yt-dlp").TryGetValue(out string? ytDlp))
        {
            return YtDlpErrors.Missing();
        }

        var arguments = new List<string>(options.CommonArguments())
        {
            "--dump-single-json", "--no-playlist", "--format", FormatSelector(maxQuality),
        };
        foreach ((string name, string value) in request.Headers.Values)
        {
            arguments.Add("--add-headers");
            arguments.Add($"{name}:{value}");
        }

        arguments.Add(ExtractorUrl(request.Url, request.ExtractorQuery));
        ProcessResult result = await runner.RunAsync(
            new ProcessSpec(ytDlp, arguments) { OnErrorLine = reporter.Detail }, cancellationToken);

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
            return YtDlpErrors.UnreadableOutput(exception.Message);
        }
    }

    /// <summary>Best video+audio up to <paramref name="maxQuality"/> lines; MP4/M4A preferred.</summary>
    public static string FormatSelector(VideoQuality maxQuality)
    {
        int height = maxQuality.Height;
        return $"bv*[height<={height}][ext=mp4]+ba[ext=m4a]/bv*[height<={height}]+ba/b[height<={height}]/bv*+ba/b";
    }

    /// <summary>
    /// Adds extra data for yt-dlp's site extractor to the URL ("smuggling", yt-dlp's own mechanism).
    /// Used to tell Dailymotion which page and player a video is embedded in.
    /// </summary>
    public static string ExtractorUrl(string url, IReadOnlyDictionary<string, string> extractorQuery)
    {
        if (extractorQuery.Count == 0)
        {
            return url;
        }

        string json = JsonSerializer.Serialize(new Dictionary<string, object> { ["query"] = extractorQuery });
        return $"{url}#__youtubedl_smuggle={Uri.EscapeDataString(json)}";
    }

    private static Result<ResolvedVideo, Error> ParseInfo(string json, string url)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (Text(root, "_type") == "playlist")
        {
            return YtDlpErrors.IsPlaylist(url);
        }

        var info = new VideoInfo(
            Text(root, "id").GetValueOrDefault(string.Empty),
            Text(root, "title").GetValueOrDefault("untitled"),
            Number(root, "duration").Bind(seconds => VideoTime.FromSeconds(seconds).ValueOrNone()),
            Text(root, "webpage_url").GetValueOrDefault(url),
            Text(root, "extractor_key").Or(Text(root, "extractor")).GetValueOrDefault(string.Empty));

        IEnumerable<JsonElement> formats = root.TryGetProperty("requested_formats", out JsonElement requested)
            && requested.ValueKind == JsonValueKind.Array
                ? requested.EnumerateArray()
                : [root];
        var streams = new List<StreamFormat>();
        foreach (JsonElement format in formats)
        {
            if (!Text(format, "url").TryGetValue(out string? streamUrl))
            {
                return YtDlpErrors.NoStreamAddress();
            }

            streams.Add(new StreamFormat(streamUrl, Text(format, "protocol").GetValueOrDefault("https"), Headers(format)));
        }

        return new ResolvedVideo(info, streams);
    }

    private static RequestHeaders Headers(JsonElement format) =>
        format.TryGetProperty("http_headers", out JsonElement headers) && headers.ValueKind == JsonValueKind.Object
            ? RequestHeaders.From(headers.EnumerateObject().Select(header => new KeyValuePair<string, string>(header.Name, header.Value.ToString())))
            : RequestHeaders.None;

    private static Maybe<string> Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? Maybe.From(value.GetString()!)
            : Maybe<string>.None;

    private static Maybe<double> Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : Maybe<double>.None;
}
