using System.Globalization;
using OuttaContext.Core.Common;
using OuttaContext.Core.Library;
using OuttaContext.Core.Reporting;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Storage;

namespace OuttaContext.Core.Uploads;

/// <summary>Uploads files, clips and episodes, skipping files already uploaded unchanged.</summary>
public sealed class UploadService(
    IStorageRegistry storages,
    IStorageBackendFactory backends,
    ILibraryRepository repository,
    OuttaSettings settings,
    IReporter reporter)
    : IClipUploader, IEpisodeUploader
{
    /// <summary>Uploads any file into the storage's folder (or a subfolder of it).</summary>
    /// <param name="reference">The clip ID or episode name the file belongs to, for the upload record.</param>
    public Task<Result<string, Error>> UploadFileAsync(
        string path,
        string? storage,
        CancellationToken cancellationToken,
        string subfolder = "",
        UploadKind kind = UploadKind.Clip,
        string? reference = null) =>
        storages.Choose(storage, settings.DefaultStorage)
            .Ensure(_ => File.Exists(path), UploadErrors.FileMissing(path))
            .Bind(config => UploadUnlessDoneAsync(config, path, subfolder, kind, reference ?? path, cancellationToken));

    public Task<Result<string, Error>> UploadClipAsync(Clip clip, string? storage, CancellationToken cancellationToken) =>
        UploadClipAsync(clip, storage, string.Empty, cancellationToken);

    public async Task<Result<IReadOnlyList<string>, Error>> UploadEpisodeAsync(
        Episode episode, IReadOnlyList<Clip> clips, string? storage, CancellationToken cancellationToken)
    {
        if (!episode.OutputPath.TryGetValue(out string? output) || !File.Exists(output))
        {
            return UploadErrors.EpisodeNotBuilt(episode);
        }

        string folder = episode.Name.FileName;
        var uploaded = new List<string>();
        Result<string, Error> video = await UploadFileAsync(output, storage, cancellationToken, folder, UploadKind.Episode, episode.Name.Value);
        if (video.IsFailure)
        {
            return video.Error;
        }

        uploaded.Add(video.Value);
        foreach (Clip clip in clips)
        {
            Result<string, Error> result = await UploadClipAsync(clip, storage, $"{folder}/clips", cancellationToken);
            if (result.IsFailure)
            {
                return result.Error;
            }

            uploaded.Add(result.Value);
        }

        return uploaded;
    }

    private Task<Result<string, Error>> UploadClipAsync(Clip clip, string? storage, string subfolder, CancellationToken cancellationToken) =>
        UploadFileAsync(clip.FilePath, storage, cancellationToken, subfolder, UploadKind.Clip, clip.Id.ToString(CultureInfo.InvariantCulture));

    private async Task<Result<string, Error>> UploadUnlessDoneAsync(
        StorageConfig config, string path, string subfolder, UploadKind kind, string reference, CancellationToken cancellationToken)
    {
        string fileName = Path.GetFileName(path);
        string storageName = config.Name.Value;
        string remoteKey = subfolder.Length > 0 ? $"{subfolder}/{fileName}" : fileName;
        if (repository.FindUpload(path, storageName, remoteKey).TryGetValue(out UploadRecord? previous))
        {
            reporter.Success($"Already uploaded {fileName} to {storageName} ({previous.UploadedAt})");
            return previous.RemotePath;
        }

        ByteSize size = ByteSize.OfFile(path);
        reporter.Step($"Uploading {fileName} ({size}) to {storageName}");
        Result<UploadedFile, Error> upload;
        using (IProgressTask progress = reporter.StartProgress(fileName, ProgressUnit.Bytes, size.Bytes))
        {
            upload = await backends.Create(config).UploadAsync(path, subfolder, progress.Report, cancellationToken);
        }

        return upload
            .Tap(_ => repository.RecordUpload(kind, reference, path, storageName, remoteKey))
            .Tap(uploaded => reporter.Success($"Uploaded to {uploaded.Location}"))
            .Map(uploaded => uploaded.Location);
    }
}
