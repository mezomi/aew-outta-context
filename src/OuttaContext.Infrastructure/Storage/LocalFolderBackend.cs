using OuttaContext.Core.Results;
using OuttaContext.Core.Storage;

namespace OuttaContext.Infrastructure.Storage;

/// <summary>Uploads by copying into another folder: an external drive, a NAS share or a synced folder.</summary>
public sealed class LocalFolderBackend(StorageConfig config) : IStorageBackend
{
    private const string Tool = "local folder";
    private const int ChunkSize = 4 * 1024 * 1024;

    public StorageConfig Config => config;

    private string Root => config.Folder;

    public async Task<ToolResult<string>> UploadAsync(
        string localPath, string subfolder, Action<double, double?> onProgressBytes, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(Root))
        {
            return new ToolError(Tool, $"Storage folder {Root} is not available.", "Plug in the drive or connect the share, then try again.");
        }

        // Subfolders are written "Episode 3/clips" (remote style); use this system's separator on disk.
        string localSubfolder = subfolder.Replace('/', Path.DirectorySeparatorChar);
        string targetDirectory = localSubfolder.Length > 0 ? Path.Combine(Root, localSubfolder) : Root;
        string target = Path.Combine(targetDirectory, Path.GetFileName(localPath));
        string partial = target + ".part";
        try
        {
            Directory.CreateDirectory(targetDirectory);
            long total = new FileInfo(localPath).Length;
            long done = 0;
            var buffer = new byte[ChunkSize];
            await using (FileStream source = File.OpenRead(localPath))
            await using (FileStream destination = File.Create(partial))
            {
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    done += read;
                    onProgressBytes(done, total);
                }
            }

            File.SetLastWriteTimeUtc(partial, File.GetLastWriteTimeUtc(localPath));
            File.Move(partial, target, overwrite: true);
            return ToolResult<string>.Success(target);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            File.Delete(partial);
            return new ToolError(Tool, $"Could not copy to {target}: {exception.Message}", "Check the folder's permissions and free space.");
        }
    }

    public Task<ToolResult<Done>> TestAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(Root))
        {
            return Task.FromResult(ToolResult.Failure(new ToolError(
                Tool, $"Folder {Root} does not exist or is not connected.", "Create the folder or connect the drive.")));
        }

        string probe = Path.Combine(Root, $".outta-test-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return Task.FromResult(ToolResult.Success());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(ToolResult.Failure(new ToolError(Tool, $"Can't write to {Root}: {exception.Message}", "Check permissions.")));
        }
    }

    public Task<ToolResult<Done>> ForgetAsync(CancellationToken cancellationToken) => Task.FromResult(ToolResult.Success());
}
