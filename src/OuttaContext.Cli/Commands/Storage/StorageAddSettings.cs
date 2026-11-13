using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Storage;

/// <summary>Options of `outta storage add NAME`.</summary>
public sealed class StorageAddSettings : GlobalSettings
{
    public const string DefaultFolder = "AEW Outta Context";

    [CommandArgument(0, "<NAME>")]
    [Description("Short name, e.g. drive.")]
    public string Name { get; init; } = string.Empty;

    [CommandOption("-p|--provider <PROVIDER>")]
    [Description("gdrive, onedrive, dropbox, b2 or local.")]
    public string? Provider { get; init; }

    [CommandOption("-f|--folder <FOLDER>")]
    [Description("Remote folder (for local: the destination path).")]
    [DefaultValue(DefaultFolder)]
    public string Folder { get; init; } = DefaultFolder;

    [CommandOption("--account <KEY_ID>")]
    [Description("Backblaze B2 key ID (not secret).")]
    public string? Account { get; init; }

    [CommandOption("--skip-test")]
    [Description("Don't test the storage after adding it.")]
    public bool SkipTest { get; init; }

    public override ValidationResult Validate() =>
        Provider is null ? ValidationResult.Error("Missing --provider (gdrive, onedrive, dropbox, b2 or local).") : base.Validate();
}
