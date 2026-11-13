using OuttaContext.Core.Sources;
using OuttaContext.TestKit.Assertions;

namespace OuttaContext.Core.Tests.Sources;

public sealed class DirectStreamSourceTests
{
    private readonly DirectStreamSource _source = new();

    [Fact]
    public void RequestVariants_DailymotionCdnLink_AsksAsThePlayerFirst()
    {
        VideoLink link = _source.Recognize("https://vod3.cf.dmcdn.net/x/manifest.m3u8").ShouldHaveValue();

        IReadOnlyList<RequestVariant> variants = link.RequestVariants("https://page");

        variants[0].Headers.ShouldBe(DailymotionSource.PlayerHeaders);
        variants[^1].Headers.ShouldBe(RequestHeaders.RefererFor("https://page"));
    }

    [Theory]
    [InlineData("https://example.com/a.m3u8", true)]
    [InlineData("https://example.com/a.MP4", true)]
    [InlineData("ftp://example.com/a.mp4", false)]
    [InlineData("https://example.com/page.html", false)]
    public void Recognize_StreamFileLinks_Only(string url, bool expected)
    {
        _source.Recognize(url).HasValue.ShouldBe(expected);
    }
}
