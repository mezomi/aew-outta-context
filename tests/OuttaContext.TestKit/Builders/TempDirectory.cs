namespace OuttaContext.TestKit.Builders;

/// <summary>A fresh folder for one test, deleted afterwards. Use with <c>using var temp = new TempDirectory();</c>.</summary>
public sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "outta-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    /// <summary>Writes a file (creating folders) and returns its full path.</summary>
    public string Write(string relativePath, string content = "x")
    {
        string path = File(relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A process may still hold a file briefly; the OS temp folder is cleaned eventually.
        }
    }
}
