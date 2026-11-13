using OuttaContext.Core.Clips;
using OuttaContext.Core.Errors;
using OuttaContext.Core.Library;
using OuttaContext.Core.Reporting;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Storage;

namespace OuttaContext.Core.Uploads;

public interface IClipUploader
{
    Task<string> UploadClipAsync(Clip clip, string? storage, CancellationToken cancellationToken, string subfolder = "");
}

public interface IEpisodeUploader
{
    Task<IReadOnlyList<string>> UploadEpisodeAsync(
        Episode episode, IReadOnlyList<Clip> clips, string? storage, CancellationToken cancellationToken);
}

/// <summary>Uploads files, clips and episodes, skipping files already uploaded unchanged.</summary>
public sealed class UploadService(
    IStorageRegistry storages, ILibraryRepository repository, OuttaSettings settings, IReporter reporter)
    : IClipUploader, IEpisodeUploader
{
    public async Task<string> UploadFileAsync(
        string path, string? storage, CancellationToken cancellationToken, string subfolder = "", string kind = "file", string? reference = null)
    {
        string name = ResolveStorageName(storage);
        if (!File.Exists(path))
        {
            throw new OuttaException($"{path} does not exist.", "Check the path or clip ID.");
        }

        string remoteKey = subfolder.Length > 0 ? $"{subfolder}/{Path.GetFileName(path)}" : Path.GetFileName(path);
        if (repository.FindUpload(path, name, remoteKey) is { } previous)
        {
            reporter.Success($"Already uploaded {Path.GetFileName(path)} to {name} ({previous.UploadedAt})");
            return previous.RemotePath;
        }

        IStorageBackend backend = storages.Backend(storages.Get(name));
        long size = new FileInfo(path).Length;
        reporter.Step($"Uploading {Path.GetFileName(path)} ({size / 1_000_000.0:0.0} MB) to {name}");
        string where;
        using (IProgressTask progress = reporter.StartProgress(Path.GetFileName(path), ProgressUnit.Bytes, size))
        {
            where = (await backend.UploadAsync(path, subfolder, progress.Report, cancellationToken)).ValueOrThrow();
        }

        repository.RecordUpload(kind, reference ?? path, path, name, remoteKey);
        reporter.Success($"Uploaded to {where}");
        return where;
    }

    public Task<string> UploadClipAsync(Clip clip, string? storage, CancellationToken cancellationToken, string subfolder = "")
    {
        if (!File.Exists(clip.FilePath))
        {
            throw new ClipFileMissingException($"The file for clip {clip.Id} is missing: {clip.FilePath}");
        }

        return UploadFileAsync(clip.FilePath, storage, cancellationToken, subfolder, "clip", clip.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public async Task<IReadOnlyList<string>> UploadEpisodeAsync(
        Episode episode, IReadOnlyList<Clip> clips, string? storage, CancellationToken cancellationToken)
    {
        if (episode.OutputPath is not { } output || !File.Exists(output))
        {
            throw new OuttaException($"\"{episode.Name}\" has no built video yet.", "Build it with `outta episode build`.");
        }

        string folder = ClipTitle.ToFileName(episode.Name);
        var uploaded = new List<string> { await UploadFileAsync(output, storage, cancellationToken, folder, "episode", episode.Name) };
        foreach (Clip clip in clips)
        {
            uploaded.Add(await UploadClipAsync(clip, storage, cancellationToken, $"{folder}/clips"));
        }

        return uploaded;
    }

    private string ResolveStorageName(string? storage)
    {
        string name = storage ?? settings.DefaultStorage
            ?? throw new StorageNotConfiguredException("No storage chosen and no default storage is set.");
        storages.Get(name);
        return name;
    }
}
