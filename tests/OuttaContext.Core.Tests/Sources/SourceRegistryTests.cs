using OuttaContext.Core.Errors;
using OuttaContext.Core.Sources;

namespace OuttaContext.Core.Tests.Sources;

public sealed class SourceRegistryTests
{
    private const string Dm = "https://www.dailymotion.com/video/";
    private const string Yt = "https://www.youtube.com/watch?v=";
    private readonly SourceRegistry _registry = new();

    [Theory]
    [InlineData("https://www.dailymotion.com/video/k1ojSwOaH7psOtMbpGS", "dailymotion", Dm + "k1ojSwOaH7psOtMbpGS")]
    [InlineData("https://www.dailymotion.com/embed/video/x8abc12?autoplay=1", "dailymotion", Dm + "x8abc12")]
    [InlineData("https://geo.dailymotion.com/player/xc394.html?video=k1ojSwOaH7psOtMbpGS", "dailymotion", Dm + "k1ojSwOaH7psOtMbpGS")]
    [InlineData("https://geo.dailymotion.com/player.html?mute=1&video=x8abc12", "dailymotion", Dm + "x8abc12")]
    [InlineData("https://dai.ly/x8abc12", "dailymotion", Dm + "x8abc12")]
    [InlineData("https://www.youtube.com/watch?feature=share&v=dQw4w9WgXcQ&t=10", "youtube", Yt + "dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?si=xyz", "youtube", Yt + "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/aqz-KE-bpKQ", "youtube", Yt + "aqz-KE-bpKQ")]
    [InlineData("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ", "youtube", Yt + "dQw4w9WgXcQ")]
    [InlineData("https://vod3.cf.dmcdn.net/sec2(x)/video/1/manifest.m3u8", "direct", "https://vod3.cf.dmcdn.net/sec2(x)/video/1/manifest.m3u8")]
    [InlineData("https://example.com/videos/clip.mp4", "direct", "https://example.com/videos/clip.mp4")]
    public void Detect_SupportedLink_ReturnsSourceAndCanonicalUrl(string url, string source, string canonical)
    {
        VideoSource detected = _registry.Detect(url);

        detected.Name.ShouldBe(source);
        detected.CanonicalUrl(url).ShouldBe(canonical);
    }

    [Theory]
    [InlineData("https://example.com/article.html")]
    [InlineData("https://vimeo.com/12345")]
    [InlineData("https://www.youtube.com/embed/videoseries?list=PL1")]
    [InlineData("not a url")]
    public void Detect_UnsupportedLink_ThrowsWithScrapeHint(string url)
    {
        Should.Throw<UnsupportedUrlException>(() => _registry.Detect(url)).Hint.ShouldContain("outta scrape");
    }
}
