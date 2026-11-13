namespace OuttaContext.Cli.Tests.Commands.Upload;

public sealed class UploadCommandTests : IDisposable
{
    private readonly CliHarness _cli = new();

    public void Dispose() => _cli.Dispose();

    [Fact]
    public async Task Upload_ClipTitle_CopiesTheClipToTheDefaultStorage()
    {
        // Arrange
        await _cli.WithClipFolderAsync();
        string backup = _cli.Temp.File("backup");
        Directory.CreateDirectory(backup);
        (await _cli.RunAsync("storage", "add", "usb", "--provider", "local", "--folder", backup)).ExitCode.ShouldBe(0);
        (await _cli.RunAsync("clip", "https://youtu.be/dQw4w9WgXcQ", "-r", "0-5", "-t", "Table spot")).ExitCode.ShouldBe(0);

        // Act
        (int code, string output) = await _cli.RunAsync("upload", "table spot");

        // Assert
        code.ShouldBe(0, output);
        File.Exists(Path.Combine(backup, "Table spot.mp4")).ShouldBeTrue();
    }

    [Fact]
    public async Task Upload_NothingByThatName_SaysWhatItTried()
    {
        (int code, string output) = await _cli.RunAsync("upload", "nothing here");

        code.ShouldBe(1);
        output.ShouldContain("not a file, a clip ID or title, or an episode name");
    }
}
