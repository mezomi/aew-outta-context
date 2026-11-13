using OuttaContext.Core.Sources;
using OuttaContext.TestKit.Assertions;

namespace OuttaContext.Core.Tests.Sources;

public sealed class DailymotionSourceTests
{
    private const string GeoPlayerUrl = "https://geo.dailymotion.com/player/xc394.html?video=k1ojSwOaH7psOtMbpGS";
    private readonly VideoLink _link = new DailymotionSource().Recognize(GeoPlayerUrl).ShouldHaveValue();

    [Fact]
    public void RequestVariants_WithPage_AsksAsTheEmbeddedPlayerFirst()
    {
        IReadOnlyList<RequestVariant> variants = _link.RequestVariants("https://site/article");

        variants.Select(v => v.Label).ShouldBe(["embedded player", "direct, page as referer", "direct"]);
        RequestVariant player = variants[0];
        player.Headers.Values["Origin"].ShouldBe("https://geo.dailymotion.com");
        player.ExtractorQuery["embedder"].ShouldBe("https://site/article");
        player.ExtractorQuery["player-id"].ShouldBe("xc394");
    }

    [Fact]
    public void RequestVariants_WithoutPage_OnlyAsksDirectly()
    {
        _link.RequestVariants(Maybe<string>.None).Select(v => v.Label).ShouldBe(["direct"]);
    }
}
