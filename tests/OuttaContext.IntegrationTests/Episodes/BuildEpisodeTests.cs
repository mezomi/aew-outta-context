using OuttaContext.Core.Episodes;
using OuttaContext.Core.Library;
using OuttaContext.Core.Media;
using OuttaContext.Core.Paths;
using OuttaContext.Core.Settings;
using OuttaContext.Infrastructure.Persistence;
using OuttaContext.IntegrationTests.Support;
using OuttaContext.TestKit.Assertions;
using OuttaContext.TestKit.Builders;
using OuttaContext.TestKit.Fakes;

namespace OuttaContext.IntegrationTests.Episodes;

[Trait("Category", "Integration")]
public sealed class BuildEpisodeTests : IDisposable
{
    private static readonly NormalizeTarget SmallTarget = new(Width: 640, Height: 360, Fps: 30, Preset: "ultrafast", Crf: 30);

    private readonly TempDirectory _temp = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        _temp.Dispose();
    }

    [Fact]
    public async Task HandleAsync_MixedClips_BecomeOneUniformEpisode()
    {
        // Arrange: clips with different sizes, frame rates, and one without audio
        TestVideos.RequireFfmpeg();
        var repository = new SqliteLibraryRepository(_temp.File("library.db"));
        var settings = new OuttaSettings { ClipFolder = _temp.File("clips") };
        var episodes = new EpisodeService(repository, settings);
        foreach ((string title, double seconds, string size, int rate, bool audio) in new[]
        {
            ("Dailymotion-ish 720p25", 3.0, "1280x720", 25, true),
            ("YouTube-ish 1080p60", 2.0, "1920x1080", 60, true),
            ("Vertical silent", 2.0, "360x640", 30, false),
        })
        {
            string path = TestVideos.Make(_temp.File("clips", title + ".mp4"), seconds, size, rate, audio);
            episodes.AddClip(repository.AddClip(Values.NewClip(title, seconds, path))).ShouldSucceed();
        }

        var reporter = new RecordingReporter();
        var real = new RealTools(_temp, reporter);
        var cache = new EpisodeCache(new AppPaths(_temp.File("data")), SmallTarget);
        var handler = new BuildEpisodeHandler(episodes, real.Ffmpeg, cache, settings, new FakeEpisodeUploader(), reporter);

        // Act
        BuildEpisodeResult result = (await handler.HandleAsync(new BuildEpisodeRequest(), Ct)).ShouldSucceed();
        (await handler.HandleAsync(new BuildEpisodeRequest("Episode 1"), Ct)).ShouldSucceed();

        // Assert
        ProbeResult probe = (await real.Ffmpeg.ProbeAsync(result.OutputPath, Ct)).ShouldSucceed();
        probe.Duration.Seconds.ShouldBe(7, 0.3);
        (probe.Width, probe.Height).ShouldBe((640, 360));
        probe.HasAudio.ShouldBeTrue();
        reporter.Texts("step").Count(step => step.Contains("using converted copy", StringComparison.Ordinal)).ShouldBe(3);
    }
}
