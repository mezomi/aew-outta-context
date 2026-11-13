using System.ComponentModel;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Errors;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Storage;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Storage;

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

public sealed class StorageNameSettings : GlobalSettings
{
    [CommandArgument(0, "<NAME>")]
    [Description("Storage name.")]
    public string Name { get; init; } = string.Empty;
}

[Description("Register a storage and sign in once (browser sign-in for Drive/OneDrive/Dropbox).")]
public sealed class StorageAddCommand(IStorageRegistry storages, ISettingsStore settingsStore, OuttaSettings settings, SpectreReporter reporter, IAnsiConsole console)
    : AsyncCommand<StorageAddSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, StorageAddSettings options, CancellationToken cancellationToken)
    {
        string name = StorageProviders.ValidateName(options.Name);
        string provider = options.Provider!.ToLowerInvariant();
        if (!StorageProviders.All.Contains(provider))
        {
            throw new SettingsException($"Unknown provider \"{provider}\".", "Use one of: " + string.Join(", ", StorageProviders.All));
        }

        if (storages.List().Any(existing => existing.Name == name))
        {
            throw new AlreadyExistsException($"A storage named \"{name}\" already exists.", "Pick another name, or remove the old one first.");
        }

        if (provider == StorageProviders.LocalFolder && options.Folder == StorageAddSettings.DefaultFolder)
        {
            throw new SettingsException("Local storage needs a folder.", "Pass --folder \"D:\\AEW backup\".");
        }

        var extra = new Dictionary<string, string>();
        if (provider == StorageProviders.Backblaze)
        {
            extra["account"] = options.Account ?? console.Prompt(new TextPrompt<string>("Backblaze key ID (not secret):"));
        }

        var config = new StorageConfig(name, provider, options.Folder, extra);
        IStorageBackend backend = storages.Backend(config);
        if (backend is ISignInStorage signIn)
        {
            if (provider != StorageProviders.Backblaze)
            {
                reporter.Step($"Opening your browser to sign in to {StorageProviders.Labels[provider]}...");
            }

            (await signIn.SignInAsync(label => console.Prompt(new TextPrompt<string>(label + ":").Secret()), cancellationToken)).ValueOrThrow();
        }

        storages.Add(config);
        reporter.Success($"Added storage {name} ({StorageProviders.Labels[provider]}, folder {options.Folder})");
        if (!options.SkipTest)
        {
            reporter.Step($"Testing {name}");
            (await backend.TestAsync(cancellationToken)).ValueOrThrow();
            reporter.Success($"{name} works");
        }

        if (settings.DefaultStorage is null)
        {
            settingsStore.Set(SettingKeys.DefaultStorage, name);
            reporter.Success($"{name} is now the default storage");
        }

        return 0;
    }
}

[Description("Show configured storages.")]
public sealed class StorageListCommand(IStorageRegistry storages, OuttaSettings settings, IAnsiConsole console) : Command<GlobalSettings>
{
    public override int Execute(CommandContext context, GlobalSettings options, CancellationToken cancellationToken)
    {
        IReadOnlyList<StorageConfig> configs = storages.List();
        if (configs.Count == 0)
        {
            console.WriteLine("No storage yet. Add one with `outta storage add drive --provider gdrive`.");
            return 0;
        }

        var table = new Table().NoBorder().AddColumns("Name", "Provider", "Folder");
        foreach (StorageConfig config in configs)
        {
            string marker = config.Name == settings.DefaultStorage ? " [cyan](default)[/]" : string.Empty;
            table.AddRow(Markup.Escape(config.Name) + marker, StorageProviders.Labels.GetValueOrDefault(config.Provider, config.Provider), Markup.Escape(config.Folder));
        }

        console.Write(table);
        return 0;
    }
}

[Description("Check a storage login works (uploads and deletes a tiny file).")]
public sealed class StorageTestCommand(IStorageRegistry storages, SpectreReporter reporter) : AsyncCommand<StorageNameSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, StorageNameSettings options, CancellationToken cancellationToken)
    {
        StorageConfig config = storages.Get(options.Name);
        reporter.Step($"Testing {options.Name}");
        (await storages.Backend(config).TestAsync(cancellationToken)).ValueOrThrow();
        reporter.Success($"{options.Name} works");
        return 0;
    }
}

[Description("Set the storage used by --upload and `outta upload`.")]
public sealed class StorageDefaultCommand(IStorageRegistry storages, ISettingsStore settingsStore, SpectreReporter reporter) : Command<StorageNameSettings>
{
    public override int Execute(CommandContext context, StorageNameSettings options, CancellationToken cancellationToken)
    {
        storages.Get(options.Name);
        settingsStore.Set(SettingKeys.DefaultStorage, options.Name);
        reporter.Success($"{options.Name} is now the default storage");
        return 0;
    }
}

[Description("Remove a storage, its login and any stored key.")]
public sealed class StorageRemoveCommand(IStorageRegistry storages, ISettingsStore settingsStore, OuttaSettings settings, SpectreReporter reporter)
    : AsyncCommand<StorageNameSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, StorageNameSettings options, CancellationToken cancellationToken)
    {
        StorageConfig config = storages.Get(options.Name);
        await storages.Backend(config).ForgetAsync(cancellationToken);
        storages.Remove(options.Name);
        if (settings.DefaultStorage == options.Name)
        {
            settingsStore.Set(SettingKeys.DefaultStorage, string.Empty);
        }

        reporter.Success($"Removed storage {options.Name}");
        return 0;
    }
}
