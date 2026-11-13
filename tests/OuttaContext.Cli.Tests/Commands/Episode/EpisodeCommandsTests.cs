using System.Text.Json;

namespace OuttaContext.Cli.Tests.Commands.Episode;

public sealed class EpisodeCommandsTests : IDisposable
{
    private readonly CliHarness _cli = new();

    public void Dispose() => _cli.Dispose();

    [Fact]
    public async Task MoveRemoveBuild_ThreeClips_WorkTogether()
    {
        // Arrange
        await _cli.WithClipFolderAsync();
        foreach (string title in new[] { "a", "b", "c" })
        {
            (await _cli.RunAsync("clip", "https://youtu.be/dQw4w9WgXcQ", "-r", "0-5", "-t", title)).ExitCode.ShouldBe(0);
        }

        // Act
        (int moved, _) = await _cli.RunAsync("episode", "move", "3", "1");
        (_, string removed) = await _cli.RunAsync("episode", "remove", "1");
        (int built, string buildOutput) = await _cli.RunAsync("episode", "build");
        (_, string list) = await _cli.RunAsync("episode", "list", "--json");

        // Assert
        moved.ShouldBe(0);
        removed.ShouldContain("file kept");
        built.ShouldBe(0, buildOutput);
        buildOutput.ShouldContain("Opened \"Episode 2\"");
        using JsonDocument json = JsonDocument.Parse(list);
        json.RootElement.EnumerateArray().Select(e => e.GetProperty("name").GetString()).ShouldBe(["Episode 1", "Episode 2"]);
    }

    [Fact]
    public async Task Move_PositionTooHigh_PrintsHint()
    {
        await _cli.WithClipFolderAsync();
        await _cli.RunAsync("clip", "https://youtu.be/dQw4w9WgXcQ", "-r", "0-5", "-t", "a");

        (int code, string output) = await _cli.RunAsync("episode", "move", "1", "9");

        code.ShouldBe(1);
        output.ShouldContain("Positions start at 1");
    }

    [Fact]
    public async Task New_DuplicateName_IsRefused()
    {
        (await _cli.RunAsync("episode", "new", "Special")).ExitCode.ShouldBe(0);

        (int code, _) = await _cli.RunAsync("episode", "new", "special");

        code.ShouldBe(1);
    }

    [Fact]
    public async Task Build_NoClips_SaysTheEpisodeIsEmpty()
    {
        (int code, string output) = await _cli.RunAsync("episode", "build");

        code.ShouldBe(1);
        output.ShouldContain("has no clips");
    }
}
