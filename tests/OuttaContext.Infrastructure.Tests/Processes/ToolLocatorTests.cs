using OuttaContext.Core.Paths;
using OuttaContext.Infrastructure.Processes;
using OuttaContext.TestKit.Assertions;
using OuttaContext.TestKit.Builders;

namespace OuttaContext.Infrastructure.Tests.Processes;

public sealed class ToolLocatorTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Find_ToolOnPath_ReturnsItsFullPath()
    {
        string onPath = _temp.Write(Path.Combine("bin", ToolLocator.ExecutableName("ffmpeg")));
        var locator = new ToolLocator(new AppPaths(_temp.File("app")), _ => _temp.File("bin"));

        locator.Find("ffmpeg").ShouldHaveValue().ShouldBe(onPath);
    }

    [Fact]
    public void Find_ManagedCopyAndPathCopy_PrefersTheManagedCopy()
    {
        var paths = new AppPaths(_temp.File("app"));
        _temp.Write(Path.Combine("bin", ToolLocator.ExecutableName("yt-dlp")));
        var locator = new ToolLocator(paths, _ => _temp.File("bin"));
        string managed = locator.ManagedPath("yt-dlp");
        Directory.CreateDirectory(Path.GetDirectoryName(managed)!);
        File.WriteAllText(managed, string.Empty);

        locator.Find("yt-dlp").ShouldHaveValue().ShouldBe(managed);
    }

    [Fact]
    public void Find_MissingTool_IsNone()
    {
        new ToolLocator(new AppPaths(_temp.File("app")), _ => string.Empty).Find("rclone").HasNoValue.ShouldBeTrue();
    }
}
