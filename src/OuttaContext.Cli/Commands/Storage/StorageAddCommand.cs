using System.ComponentModel;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Common;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Storage;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Storage;

/// <summary>
/// `outta storage add NAME --provider P`: check the name and provider → sign in → save → test →
/// make it the default if there is none yet. Each step stops the chain if it fails.
/// </summary>
[Description("Register a storage and sign in once (browser sign-in for Drive/OneDrive/Dropbox).")]
public sealed class StorageAddCommand(
    IStorageRegistry storages,
    IStorageBackendFactory backends,
    ISettingsStore settingsStore,
    OuttaSettings settings,
    SpectreReporter reporter,
    IAnsiConsole console)
    : AsyncCommand<StorageAddSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, StorageAddSettings options, CancellationToken cancellationToken) =>
        StorageName.Create(options.Name)
            .Ensure(name => storages.Find(name).HasNoValue, name => StorageErrors.AlreadyExists(name))
            .Bind(name => StorageProvider.Parse(options.Provider!).Map(provider => (name, provider)))
            .Ensure(
                chosen => chosen.provider != StorageProvider.LocalFolder || options.Folder != StorageAddSettings.DefaultFolder,
                chosen => StorageErrors.FolderRequired(chosen.provider))
            .Map(chosen => new StorageConfig(chosen.name, chosen.provider, options.Folder, ExtraOptions(chosen.provider, options)))
            .Bind(config => SignInAsync(config, cancellationToken))
            .Check(storages.Add)
            .Tap(config => reporter.Success($"Added storage {config.Name} ({config.Provider.Label}, folder {config.Folder})"))
            .Bind(config => options.SkipTest ? Task.FromResult(Result.Success<StorageConfig, Error>(config)) : TestAsync(config, cancellationToken))
            .Bind(MakeDefaultIfNone)
            .ToExitCode(reporter);

    /// <summary>Backblaze needs a key ID (not secret); asked for if --account wasn't given.</summary>
    private Dictionary<string, string> ExtraOptions(StorageProvider provider, StorageAddSettings options)
    {
        var extra = new Dictionary<string, string>();
        if (provider == StorageProvider.Backblaze)
        {
            extra[StorageConfig.AccountOption] = options.Account ?? console.Prompt(new TextPrompt<string>("Backblaze key ID (not secret):"));
        }

        return extra;
    }

    private async Task<Result<StorageConfig, Error>> SignInAsync(StorageConfig config, CancellationToken cancellationToken)
    {
        if (backends.Create(config) is not ISignInStorage signIn)
        {
            return config;
        }

        if (config.Provider.SignsInWithBrowser)
        {
            reporter.Step($"Opening your browser to sign in to {config.Provider.Label}...");
        }

        UnitResult<Error> signedIn = await signIn.SignInAsync(label => console.Prompt(new TextPrompt<string>(label + ":").Secret()), cancellationToken);
        return signedIn.Map(() => config);
    }

    private async Task<Result<StorageConfig, Error>> TestAsync(StorageConfig config, CancellationToken cancellationToken)
    {
        reporter.Step($"Testing {config.Name}");
        UnitResult<Error> test = await backends.Create(config).TestAsync(cancellationToken);
        return test.Tap(() => reporter.Success($"{config.Name} works")).Map(() => config);
    }

    private Result<StorageConfig, Error> MakeDefaultIfNone(StorageConfig config)
    {
        if (settings.DefaultStorage.HasValue)
        {
            return config;
        }

        return settingsStore.Change(SettingKeys.DefaultStorage, config.Name.Value)
            .Tap(_ => reporter.Success($"{config.Name} is now the default storage"))
            .Map(_ => config);
    }
}
