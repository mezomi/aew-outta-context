using OuttaContext.Core.Paths;
using OuttaContext.Infrastructure.Processes;
using OuttaContext.TestKit.Builders;

namespace OuttaContext.Infrastructure.Tests.Fakes;

/// <summary>A <see cref="ToolLocator"/> that "finds" the named tools (empty files in a temp tools folder) and nothing on PATH.</summary>
public static class InstalledTools
{
    public static ToolLocator Only(TempDirectory temp, params string[] names)
    {
        var paths = new AppPaths(temp.File("app"));
        var locator = new ToolLocator(paths, _ => string.Empty);
        foreach (string name in names)
        {
            string path = locator.ManagedPath(name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
        }

        return locator;
    }
}
