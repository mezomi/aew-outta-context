using OuttaContext.Core.Sources;

namespace OuttaContext.Core.Tests.Sources;

public sealed class DailymotionSourceTests
{
    private const string GeoPlayerUrl = "https://geo.dailymotion.com/player/xc394.html?video=k1ojSwOaH7psOtMbpGS";
    private readonly DailymotionSource _source = new();

    [Fact]
    public void RequestVariants_WithPage_AsksAsTheEmbeddedPlayerFirst()
    {
        IReadOnlyList<RequestVariant> variants = _source.RequestVariants(GeoPlayerUrl, "https://site/article");

        variants.Select(v => v.Label).ShouldBe(["embedded player", "direct, page as referer", "direct"]);
        RequestVariant player = variants[0];
        player.Headers["Origin"].ShouldBe("https://geo.dailymotion.com");
        player.ExtractorQuery.ShouldNotBeNull();
        player.ExtractorQuery["embedder"].ShouldBe("https://site/article");
        player.ExtractorQuery["player-id"].ShouldBe("xc394");
    }

    [Fact]
    public void RequestVariants_WithoutPage_OnlyAsksDirectly()
    {
        _source.RequestVariants(GeoPlayerUrl, null).Select(v => v.Label).ShouldBe(["direct"]);
    }
}
