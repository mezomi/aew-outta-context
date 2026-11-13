using OuttaContext.Core.Common;
using OuttaContext.Core.Media;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Sources;
using OuttaContext.Core.Time;

namespace OuttaContext.TestKit.Fakes;

/// <summary>Pretends to be yt-dlp. Set <see cref="Refusals"/> to make the first N requests fail like an HTTP 403.</summary>
public sealed class FakeVideoResolver : IVideoResolver
{
    /// <summary>The video's length in seconds; null for a site that doesn't say.</summary>
    public double? Duration { get; set; } = 600;

    public string Title { get; set; } = "AEW Dynamite highlights";

    public int Refusals { get; set; }

    public List<(RequestVariant Request, VideoQuality MaxQuality)> Requests { get; } = [];

    public Task<Result<ResolvedVideo, Error>> ResolveAsync(RequestVariant request, VideoQuality maxQuality, CancellationToken cancellationToken)
    {
        Requests.Add((request, maxQuality));
        if (Refusals > 0)
        {
            Refusals--;
            return Task.FromResult(Result.Failure<ResolvedVideo, Error>(new Error("yt-dlp.forbidden", "HTTP Error 403: Forbidden", "try again")));
        }

        Maybe<VideoTime> duration = Duration is { } seconds ? VideoTime.FromTrusted(seconds) : Maybe<VideoTime>.None;
        var video = new ResolvedVideo(
            new VideoInfo("vid", Title, duration, request.Url, "Fake"),
            [new StreamFormat(request.Url, "https", request.Headers)]);
        return Task.FromResult(Result.Success<ResolvedVideo, Error>(video));
    }
}
