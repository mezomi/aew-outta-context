namespace OuttaContext.TestKit.Builders;

/// <summary>Files under tests/fixtures (saved web pages, HLS playlists).</summary>
public static class Fixtures
{
    public static string Directory { get; } = FindFixturesDirectory();

    public static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Directory, .. parts]));

    private static string FindFixturesDirectory()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            string candidate = Path.Combine(folder.FullName, "tests", "fixtures");
            if (System.IO.Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("tests/fixtures not found above " + AppContext.BaseDirectory);
    }
}
