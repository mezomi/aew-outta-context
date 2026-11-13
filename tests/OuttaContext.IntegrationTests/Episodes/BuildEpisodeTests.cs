using OuttaContext.Core.Episodes;
using OuttaContext.Core.Library;
using OuttaContext.Core.Media;
using OuttaContext.Core.Settings;
using OuttaContext.Infrastructure.Persistence;
using OuttaContext.IntegrationTests.Support;
using OuttaContext.TestKit.Builders;
using OuttaContext.TestKit.Fakes;

namespace OuttaContext.IntegrationTests.Episodes;

[Trait("Category", "Integration")]
public sealed class BuildEpisodeTests : IDisposable
{
    private static readonly NormalizeTarget SmallTarget = new(Width: 640, Height: 360, Fps: 30, Preset: "ultrafast", Crf: 30);

    private readonly TempDirectory _temp = new();

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
        var episodes = new EpisodeService(repository, 600);
        foreach ((string title, double seconds, string size, int rate, bool audio) in new[]
        {
            ("Dailymotion-ish 720p25", 3.0, "1280x720", 25, true),
            ("YouTube-ish 1080p60", 2.0, "1920x1080", 60, true),
            ("Vertical silent", 2.0, "360x640", 30, false),
        })
        {
            string path = TestVideos.Make(_temp.File("clips", title + ".mp4"), seconds, size, rate, audio);
            episodes.AddClip(repository.AddClip(new NewClip(title, "test", "u", 0, seconds, seconds, path, null)));
        }

        var reporter = new RecordingReporter();
        var real = new RealTools(_temp);
        var handler = new BuildEpisodeHandler(episodes, real.Ffmpeg, new OuttaSettings { ClipFolder = _temp.File("clips") }, _temp.File("cache"), reporter, target: SmallTarget);

        // Act
        BuildEpisodeResult result = await handler.HandleAsync(new BuildEpisodeRequest(), CancellationToken.None);
        await handler.HandleAsync(new BuildEpisodeRequest("Episode 1"), CancellationToken.None);

        // Assert
        ProbeResult probe = (await real.Ffmpeg.ProbeAsync(result.OutputPath, CancellationToken.None)).ValueOrThrow();
        probe.Duration.ShouldBe(7, 0.3);
        (probe.Width, probe.Height).ShouldBe((640, 360));
        probe.HasAudio.ShouldBeTrue();
        reporter.Texts("step").Count(step => step.Contains("using converted copy", StringComparison.Ordinal)).ShouldBe(3);
    }
}
