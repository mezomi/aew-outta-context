using OuttaContext.Core.Common;
using OuttaContext.TestKit.Assertions;

namespace OuttaContext.Core.Tests.Common;

public sealed class WebUrlTests
{
    [Theory]
    [InlineData("https://example.com/page")]
    [InlineData(" http://example.com ")]
    public void Create_HttpAddress_Succeeds(string text)
    {
        WebUrl.Create(text).ShouldSucceed().Host.ShouldBe("example.com");
    }

    [Theory]
    [InlineData("example.com/page")]
    [InlineData("ftp://example.com/file")]
    [InlineData("")]
    public void Create_NotAWebAddress_Fails(string text)
    {
        WebUrl.Create(text).ShouldFailWith("url.invalid").Hint.ShouldContain("https://");
    }

    [Fact]
    public void Resolve_RelativeLink_UsesThisAddressAsBase()
    {
        WebUrl.FromTrusted("https://cdn.example.com/hls/media.m3u8").Resolve("seg-1.ts").Value
            .ShouldBe("https://cdn.example.com/hls/seg-1.ts");
    }
}
