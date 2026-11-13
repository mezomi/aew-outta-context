using OuttaContext.Core.Common;
using OuttaContext.Core.Storage;

namespace OuttaContext.Infrastructure.Storage;

/// <summary>Errors from uploading: rclone and local folders.</summary>
public static class StorageBackendErrors
{
    public static Error RcloneMissing() =>
        new("rclone.missing", "rclone was not found.", "Install it: `winget install Rclone.Rclone`, then open a new terminal.");

    public static Error NoKeyEntered() =>
        new("rclone.no_key", "No key entered.", "Create an application key in the Backblaze console.");

    public static Error SignInUnfinished(StorageConfig config) =>
        new("rclone.sign_in", $"Signing in to {config.Provider.Label} did not finish.", "Run the command again and approve access in the browser.");

    public static Error TestFailed(StorageConfig config, string details) =>
        new(
            "rclone.test_failed",
            $"Storage \"{config.Name}\" did not work: {details}",
            "The login may have expired or been revoked. Remove it with `outta storage remove NAME` and add it again.");

    public static Error UploadFailed(StorageConfig config, string message, bool loginProblem) =>
        loginProblem
            ? new("rclone.refused", $"Upload to {config.Name} was refused: {message}", "The login may have expired. Remove the storage and add it again.")
            : new("rclone.upload_failed", $"Upload to {config.Name} failed: {message}", "Check your connection and run `outta storage test NAME`.");

    public static Error FolderUnavailable(string folder) =>
        new("local.unavailable", $"Storage folder {folder} is not available.", "Plug in the drive or connect the share, then try again.");

    public static Error FolderMissing(string folder) =>
        new("local.missing", $"Folder {folder} does not exist or is not connected.", "Create the folder or connect the drive.");

    public static Error CopyFailed(string target, string problem) =>
        new("local.copy_failed", $"Could not copy to {target}: {problem}", "Check the folder's permissions and free space.");

    public static Error NotWritable(string folder, string problem) =>
        new("local.not_writable", $"Can't write to {folder}: {problem}", "Check permissions.");
}
