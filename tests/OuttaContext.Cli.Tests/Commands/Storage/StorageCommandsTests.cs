namespace OuttaContext.Cli.Tests.Commands.Storage;

public sealed class StorageCommandsTests : IDisposable
{
    private readonly CliHarness _cli = new();

    public void Dispose() => _cli.Dispose();

    [Fact]
    public async Task LocalStorage_AddUploadRemove_WorksEndToEnd()
    {
        // Arrange
        string nas = _cli.Temp.File("nas");
        Directory.CreateDirectory(nas);
        await _cli.WithClipFolderAsync();

        // Act
        (int added, string addOutput) = await _cli.RunAsync("storage", "add", "nas", "--provider", "local", "--folder", nas);
        (int clipped, _) = await _cli.RunAsync("clip", "https://youtu.be/dQw4w9WgXcQ", "-r", "0-5", "-t", "Up", "--upload");
        (_, string again) = await _cli.RunAsync("upload", "1");
        (_, string listed) = await _cli.RunAsync("storage", "list");
        (int removed, _) = await _cli.RunAsync("storage", "remove", "nas");

        // Assert
        added.ShouldBe(0, addOutput);
        addOutput.ShouldContain("nas works");
        addOutput.ShouldContain("default storage");
        clipped.ShouldBe(0);
        File.Exists(Path.Combine(nas, "Up.mp4")).ShouldBeTrue();
        again.ShouldContain("Already uploaded");
        listed.ShouldContain("(default)");
        removed.ShouldBe(0);
        (await _cli.RunAsync("storage", "list")).Output.ShouldContain("No storage yet");
    }

    [Theory]
    [InlineData("storage", "add", "x", "--provider", "ftp")]
    [InlineData("storage", "add", "bad name!", "--provider", "local", "--folder", "/tmp")]
    [InlineData("storage", "default", "nope")]
    public async Task Storage_InvalidRequest_FailsWithAHint(params string[] args)
    {
        (int code, string output) = await _cli.RunAsync(args);

        code.ShouldBe(1);
        output.ShouldContain("hint:");
    }

    [Fact]
    public async Task StorageAdd_NoProvider_IsAUsageError()
    {
        (await _cli.RunAsync("storage", "add", "x")).ExitCode.ShouldBe(2);
    }
}
