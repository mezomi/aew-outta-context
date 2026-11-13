using OuttaContext.Core.Media;
using OuttaContext.Core.Results;
using OuttaContext.Core.Sources;

namespace OuttaContext.TestKit.Fakes;

/// <summary>Pretends to be yt-dlp. Set <see cref="Refusals"/> to make the first N requests fail like an HTTP 403.</summary>
public sealed class FakeVideoResolver : IVideoResolver
{
    public double? Duration { get; set; } = 600;

    public string Title { get; set; } = "AEW Dynamite highlights";

    public int Refusals { get; set; }

    public List<(RequestVariant Request, int MaxQuality)> Requests { get; } = [];

    public Task<ToolResult<ResolvedVideo>> ResolveAsync(
        RequestVariant request, int maxQuality, Action<string> detail, CancellationToken cancellationToken)
    {
        Requests.Add((request, maxQuality));
        if (Refusals > 0)
        {
            Refusals--;
            return Task.FromResult(ToolResult<ResolvedVideo>.Failure(new ToolError("yt-dlp", "HTTP Error 403: Forbidden", "try again")));
        }

        var video = new ResolvedVideo(
            new VideoInfo("vid", Title, Duration, request.Url, "Fake"),
            [new StreamFormat(request.Url, "https", request.Headers)]);
        return Task.FromResult(ToolResult<ResolvedVideo>.Success(video));
    }
}
