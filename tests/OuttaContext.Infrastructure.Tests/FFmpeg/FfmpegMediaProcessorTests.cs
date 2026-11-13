using OuttaContext.Core.Media;
using OuttaContext.Core.Time;
using OuttaContext.Infrastructure.FFmpeg;
using OuttaContext.Infrastructure.Processes;
using OuttaContext.Infrastructure.Tests.Fakes;
using OuttaContext.TestKit.Assertions;
using OuttaContext.TestKit.Builders;
using OuttaContext.TestKit.Fakes;

namespace OuttaContext.Infrastructure.Tests.FFmpeg;

public sealed class FfmpegMediaProcessorTests : IDisposable
{
    private const string ProbeJson = """
        {"format": {"duration": "20.04"}, "streams": [
          {"codec_type": "video", "width": 1280, "height": 720, "avg_frame_rate": "30000/1001"},
          {"codec_type": "audio"}]}
        """;

    private readonly TempDirectory _temp = new();
    private readonly FakeProcessRunner _runner = new();
    private readonly RecordingReporter _reporter = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task ProbeAsync_FfprobeJson_ReadsLengthSizeRateAndAudio()
    {
        _runner.Respond = _ => new FakeRun { OutputLines = [ProbeJson] };

        ProbeResult probe = (await Processor().ProbeAsync("a.mp4", Ct)).ShouldSucceed();

        probe.Duration.Seconds.ShouldBe(20.04);
        (probe.Width, probe.Height, probe.HasAudio).ShouldBe((1280, 720, true));
        probe.Fps!.Value.ShouldBe(29.97, 0.01);
    }

    [Fact]
    public async Task ProbeAsync_FfmpegNotInstalled_SaysHowToInstall()
    {
        var processor = new FfmpegMediaProcessor(_runner, InstalledTools.Only(_temp), _reporter);

        var result = await processor.ProbeAsync("a.mp4", Ct);

        processor.IsAvailable.ShouldBeFalse();
        result.ShouldFailWith("ffmpeg.missing").Hint.ShouldContain("winget install ffmpeg");
    }

    [Fact]
    public void NormalizeArguments_ScalesPadsAndSetsFrameRate()
    {
        var arguments = FfmpegMediaProcessor.NormalizeArguments("in.mp4", "out.mp4", new NormalizeTarget(), hasAudio: true);

        string filter = arguments[arguments.ToList().IndexOf("-vf") + 1];
        filter.ShouldContain("scale=1920:1080:force_original_aspect_ratio=decrease");
        filter.ShouldContain("pad=1920:1080");
        filter.ShouldContain("fps=30");
        arguments.ShouldNotContain("-shortest");
    }

    [Fact]
    public void NormalizeArguments_SilentClip_GetsASilentTrack()
    {
        var arguments = FfmpegMediaProcessor.NormalizeArguments("in.mp4", "out.mp4", new NormalizeTarget(), hasAudio: false);

        arguments.ShouldContain("anullsrc=r=48000:cl=stereo");
        arguments.ShouldContain("-shortest");
    }

    [Fact]
    public void ConcatList_PathWithQuote_IsEscaped()
    {
        string list = FfmpegMediaProcessor.ConcatList([_temp.File("it's.mp4")]);

        list.ShouldContain(@"it'\''s.mp4");
    }

    [Fact]
    public void CutArguments_TwoLocalInputs_SeekEachAndMapVideoAndAudio()
    {
        var arguments = FfmpegMediaProcessor.CutArguments([new CutInput("video.mp4", Values.Time(4.5)), new CutInput("audio.mp4", Values.Time(3.25))], "out.mp4", Values.Time(8)).ToList();

        arguments[arguments.IndexOf("-i") - 1].ShouldBe("4.500");
        arguments.ShouldContain("1:a:0");
        arguments[arguments.IndexOf("-t") + 1].ShouldBe("8.000");
        arguments.ShouldNotContain("-reconnect");
        arguments[^3..^1].ShouldBe(["-progress", "pipe:1"]);
    }

    [Fact]
    public void CutArguments_RemoteInput_SendsHeadersAndReconnects()
    {
        var input = new CutInput("https://cdn/v.mp4", Values.Time(90), Values.Headers(("Referer", "https://page")));

        var arguments = FfmpegMediaProcessor.CutArguments([input], "out.mp4", Values.Time(5)).ToList();

        arguments[arguments.IndexOf("-headers") + 1].ShouldBe("Referer: https://page\r\n");
        arguments.ShouldContain("-reconnect");
    }

    [Fact]
    public void DescribeCommand_Headers_AreHidden()
    {
        string shown = FfmpegMediaProcessor.DescribeCommand("/usr/bin/ffmpeg", ["-headers", "Cookie: secret\r\n", "-i", "x"]);

        shown.ShouldNotContain("secret");
        shown.ShouldContain("<headers>");
    }

    [Fact]
    public async Task CutAsync_Progress_IsReportedInSecondsAndTheFileMovedIntoPlace()
    {
        // Arrange
        _runner.Respond = spec => new FakeRun
        {
            OutputLines = ["frame=10", "out_time_us=4000000", "progress=end"],
            SideEffect = s => File.WriteAllText(s.Arguments[^1], "clip"),
        };
        var seconds = new List<double>();
        string output = _temp.File("clips", "Steve the Clown.mp4");

        // Act
        var result = await Processor().CutAsync([new CutInput("in.mp4", VideoTime.Zero)], output, Values.Time(8), seconds.Add, Ct);

        // Assert
        result.ShouldSucceed();
        File.ReadAllText(output).ShouldBe("clip");
        seconds.ShouldBe([4.0, 8.0]);
        _runner.Runs[0].StallTimeout.HasValue.ShouldBeTrue();
    }

    [Fact]
    public async Task CutAsync_FfmpegFails_ReturnsItsErrorAndLeavesNoPartialFile()
    {
        _runner.Respond = spec => new FakeRun(1)
        {
            ErrorLines = ["Opening input", "Server returned 403 Forbidden"],
            SideEffect = s => File.WriteAllText(s.Arguments[^1], "partial"),
        };

        var result = await Processor().CutAsync([new CutInput("in.mp4", VideoTime.Zero)], _temp.File("out.mp4"), Values.Time(8), _ => { }, Ct);

        result.ShouldFailWith("ffmpeg.failed").Message.ShouldContain("403 Forbidden");
        Directory.GetFiles(_temp.Path).ShouldBeEmpty();
    }

    [Fact]
    public async Task CutAsync_FfmpegStalls_SaysItWasStopped()
    {
        _runner.Respond = _ => new FakeRun(-1) { Stalled = true };

        var result = await Processor().CutAsync([new CutInput("in.mp4", VideoTime.Zero)], _temp.File("out.mp4"), Values.Time(8), _ => { }, Ct);

        result.ShouldFailWith("ffmpeg.stalled").Hint.ShouldContain("-v");
    }

    [Fact]
    public async Task ProbeAsync_UnreadableJson_FailsInsteadOfThrowing()
    {
        _runner.Respond = _ => new FakeRun { OutputLines = ["{oops"] };

        (await Processor().ProbeAsync("a.mp4", Ct)).ShouldFailWith("ffprobe.unreadable");
    }

    [Fact]
    public async Task CutAsync_Verbose_LogsTheCommandWithHeadersHidden()
    {
        _runner.Respond = _ => new FakeRun { SideEffect = s => File.WriteAllText(s.Arguments[^1], "clip") };
        var input = new CutInput("https://cdn/v.mp4", Values.Time(1), Values.Headers(("Cookie", "secret")));

        await Processor().CutAsync([input], _temp.File("out.mp4"), Values.Time(2), _ => { }, Ct);

        string logged = _reporter.Texts("detail").ShouldHaveSingleItem();
        logged.ShouldStartWith("$ ffmpeg");
        logged.ShouldNotContain("secret");
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private FfmpegMediaProcessor Processor() => new(_runner, InstalledTools.Only(_temp, "ffmpeg", "ffprobe"), _reporter);
}
