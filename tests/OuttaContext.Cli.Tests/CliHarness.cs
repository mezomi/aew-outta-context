using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OuttaContext.Cli.Composition;
using OuttaContext.Core.Doctor;
using OuttaContext.Core.Media;
using OuttaContext.Core.Paths;
using OuttaContext.Core.Reporting;
using OuttaContext.Core.Scraping;
using OuttaContext.Core.Storage;
using OuttaContext.TestKit.Builders;
using OuttaContext.TestKit.Fakes;
using Spectre.Console.Testing;

namespace OuttaContext.Cli.Tests;

/// <summary>
/// Runs `outta ARGS` exactly like the real program, except the outside world (yt-dlp, ffmpeg,
/// web, keychain) is replaced by fakes. Data files go to a temp folder that lives for one test.
/// </summary>
public sealed class CliHarness : IDisposable
{
    private readonly TempDirectory _temp = new();

    public CliHarness()
    {
        Paths = new AppPaths(_temp.File("app"));
        Fetcher.Pages["https://news/a"] = Fixtures.Read("pages", "dailymotion_article.html");
        Fetcher.Pages["https://news/empty"] = Fixtures.Read("pages", "no_videos.html");
    }

    public AppPaths Paths { get; }

    public string ClipFolder => _temp.File("clips");

    public FakeVideoResolver Resolver { get; } = new();

    public FakeClipDownloader Downloader { get; } = new();

    public FakeMediaProcessor Media { get; } = new();

    public FakePageFetcher Fetcher { get; } = new();

    public InMemoryCredentialStore Keychain { get; } = new();

    public TempDirectory Temp => _temp;

    /// <summary>Runs one command; returns the exit code and everything it printed.</summary>
    public async Task<(int ExitCode, string Output)> RunAsync(params string[] args)
    {
        var console = new TestConsole().Width(200);
        IServiceCollection services = OuttaServices.Create(console, Paths);
        services.Replace(ServiceDescriptor.Singleton<IVideoResolver>(Resolver));
        services.Replace(ServiceDescriptor.Singleton<IClipDownloader>(provider =>
        {
            Downloader.Reporter = provider.GetRequiredService<IReporter>();
            return Downloader;
        }));
        services.Replace(ServiceDescriptor.Singleton<IMediaProcessor>(Media));
        services.Replace(ServiceDescriptor.Singleton<IPageFetcher>(Fetcher));
        services.Replace(ServiceDescriptor.Singleton<ICredentialStore>(Keychain));
        services.Replace(ServiceDescriptor.Singleton<IEnvironmentInspector>(new ReadyEnvironment()));

        int exitCode = await OuttaApp.Create(services).RunAsync(GlobalOptions.MoveAfterCommand(args), TestContext.Current.CancellationToken);
        return (exitCode, console.Output);
    }

    /// <summary>Sets the clip folder to the temp folder (most tests start with this).</summary>
    public async Task<CliHarness> WithClipFolderAsync()
    {
        (int code, string output) = await RunAsync("config", "set-destination", ClipFolder);
        code.ShouldBe(0, output);
        return this;
    }

    public void Dispose() => _temp.Dispose();

    private sealed class ReadyEnvironment : IEnvironmentInspector
    {
        public DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

        public Maybe<string> FindProgram(string name) => "/bin/" + name;

        public Task<Maybe<string>> GetYtDlpVersionAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Maybe.From(DateTime.Today.ToString("yyyy.MM.dd", System.Globalization.CultureInfo.InvariantCulture)));

        public Task<IReadOnlyList<string>> GetImpersonationClientsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(["chrome", "firefox"]);
    }
}
