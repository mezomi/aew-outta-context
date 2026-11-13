using OuttaContext.Core.Sources;

namespace OuttaContext.Core.Tests.Sources;

public sealed class DirectStreamSourceTests
{
    private readonly DirectStreamSource _source = new();

    [Fact]
    public void RequestVariants_DailymotionCdnLink_AsksAsThePlayerFirst()
    {
        var variants = _source.RequestVariants("https://vod3.cf.dmcdn.net/x/manifest.m3u8", "https://page");

        variants[0].Headers["Referer"].ShouldBe("https://geo.dailymotion.com/");
        variants[^1].Headers.ShouldBe(new Dictionary<string, string> { ["Referer"] = "https://page" });
    }

    [Theory]
    [InlineData("https://example.com/a.m3u8", true)]
    [InlineData("https://example.com/a.MP4", true)]
    [InlineData("ftp://example.com/a.mp4", false)]
    [InlineData("https://example.com/page.html", false)]
    public void Matches_StreamFileLinks_Only(string url, bool expected)
    {
        _source.Matches(url).ShouldBe(expected);
    }
}
