using OuttaContext.Infrastructure.Processes;
using OuttaContext.IntegrationTests.Support;

namespace OuttaContext.IntegrationTests.Processes;

[Trait("Category", "Integration")]
public sealed class ProcessRunnerStallTests
{
    [Fact]
    public async Task RunAsync_ProgramPrintsNothing_IsStoppedAfterTheStallTimeout()
    {
        // Arrange: ffmpeg encoding silence for 60s with no output at all
        TestVideos.RequireFfmpeg();
        var spec = new ProcessSpec("ffmpeg", ["-loglevel", "quiet", "-re", "-f", "lavfi", "-i", "anullsrc", "-t", "60", "-f", "null", "-"])
        {
            StallTimeout = TimeSpan.FromSeconds(1),
        };

        // Act
        DateTime started = DateTime.UtcNow;
        ProcessResult result = await new ProcessRunner().RunAsync(spec, CancellationToken.None);

        // Assert
        result.Stalled.ShouldBeTrue();
        (DateTime.UtcNow - started).ShouldBeLessThan(TimeSpan.FromSeconds(15));
    }
}
