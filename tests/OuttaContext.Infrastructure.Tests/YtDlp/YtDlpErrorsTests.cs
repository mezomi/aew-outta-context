using OuttaContext.Infrastructure.YtDlp;

namespace OuttaContext.Infrastructure.Tests.YtDlp;

public sealed class YtDlpErrorsTests
{
    [Theory]
    [InlineData("ERROR: geo restricted: not available in your country", "yt-dlp.geo_blocked", "blocked in your country")]
    [InlineData("ERROR: Private video", "yt-dlp.unavailable", "not available")]
    [InlineData("ERROR: Sign in to confirm you're not a bot", "yt-dlp.login_required", "login")]
    [InlineData("ERROR: connection reset by peer", "yt-dlp.network", "Network problem")]
    [InlineData("ERROR: [dailymotion] k1oj: No video formats found!", "yt-dlp.forbidden", "refused the video stream")]
    [InlineData("ERROR: impersonate targets are available: firefox", "yt-dlp.impersonation", "retry as a browser")]
    [InlineData("ERROR: Could not copy Chrome cookie database", "yt-dlp.cookies", "cookies")]
    [InlineData("ERROR: something new", "yt-dlp.failed", "Download failed")]
    public void Translate_KnownError_GivesCodePlainMessageAndHint(string output, string code, string expected)
    {
        var error = YtDlpErrors.Translate(output);

        error.Code.ShouldBe(code);
        error.Message.ShouldContain(expected);
        error.Hint.ShouldNotBeEmpty();
        error.Details.ShouldBe(output);
    }

    [Fact]
    public void Translate_ProxyErrorWithConfirmBoilerplate_IsANetworkProblem()
    {
        const string Output = "ERROR: [youtube] x: Unable to download API page: ('Unable to connect to proxy') "
            + "(caused by ProxyError()); please report this issue on https://github.com/yt-dlp/yt-dlp , "
            + "Confirm you are on the latest version using yt-dlp -U";

        YtDlpErrors.Translate(Output).Message.ShouldStartWith("Network problem");
    }

    [Fact]
    public void Clean_ColoredErrorWithBoilerplate_KeepsJustTheProblem()
    {
        const string Output = "[debug] noise\n\u001b[0;31mERROR:\u001b[0m bad thing; please report this issue on https://x , etc";

        YtDlpErrors.Clean(Output).ShouldBe("bad thing");
    }
}
