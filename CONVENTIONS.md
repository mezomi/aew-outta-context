# Conventions

How this codebase is organised and why. If you're new to the project (or to the libraries it uses),
start here.

## 1. The big picture

```
src/
  OuttaContext.Core/            What the app does. Plain C#, no outside libraries.
  OuttaContext.Infrastructure/  How it talks to the outside world: yt-dlp, ffmpeg, rclone, SQLite, TOML, the keychain.
  OuttaContext.Cli/             The `outta` command: Spectre.Console.Cli commands and the console output.
tests/
  OuttaContext.TestKit/         Shared fakes and builders.
  OuttaContext.Core.Tests/      Mirrors src/OuttaContext.Core folder by folder.
  OuttaContext.Infrastructure.Tests/
  OuttaContext.Cli.Tests/
  OuttaContext.IntegrationTests/ Real ffmpeg + yt-dlp against videos generated on the fly.
  fixtures/                     Saved web pages and HLS playlists used by tests.
agents/skills/                  Instructions for AI coding assistants (.NET, .NET 11, testing, xUnit).
```

Dependencies only point inward: **Cli → Core ← Infrastructure**. Core never references a library,
so every rule in it can be tested without a network, a disk or a process.

## 2. Outside programs never throw: `ToolResult<T>`

yt-dlp, ffmpeg, rclone and websites fail for ordinary reasons: no internet, a video was removed, a
site blocked us. So every method that runs one returns a **`ToolResult<T>`** instead of throwing:

```csharp
ToolResult<ResolvedVideo> result = await resolver.ResolveAsync(request, maxQuality, reporter.Detail, ct);
if (result.Failed)
{
    reporter.Detail($"That didn't work: {result.Error.Message}");
    // ...try another way, or give up:
}
ResolvedVideo video = result.Value;            // only after checking
ResolvedVideo video = result.ValueOrThrow();   // or: "I have nothing else to try"
```

A `ToolError` always has a **Message** (what went wrong, in plain words), a **Hint** (how the user
fixes it) and optional **Details** (the program's own output, shown with `--verbose`).

The raw error text of each program is translated in **one place** per program:
`YtDlpErrors` (a table of rules, first match wins) and `FfmpegMediaProcessor.Failed`.
To handle a new yt-dlp message, add a row to `YtDlpErrors.Rules`; nothing else changes.

## 3. Our own rules throw `OuttaException`

When the user asks for something that breaks one of our rules (a range longer than the cap, a
duplicate title), throw an `OuttaException` subclass from `Core/Errors`. Each one has a default hint.

The CLI turns **both** kinds of failure into the same output, in one place (`ErrorHandler`):

```
error: 1:30-11:52 is 10:22 long, over the 45s clip cap.
hint: Check the range for a typo. ...
```

Exit codes: `0` ok, `1` a problem the user can fix, `2` wrong options (usage), `130` Ctrl+C.

## 4. Every library sits behind our own interface

| Library / program | Our interface (Core) | Adapter (Infrastructure / Cli) |
|---|---|---|
| Spectre.Console | `IReporter`, `IProgressTask` | `SpectreReporter`, `LiveProgressTask` |
| yt-dlp | `IVideoResolver`, `IClipDownloader` | `YtDlpVideoResolver`, `YtDlpStreamFetcher`, `ClipDownloader` |
| ffmpeg / ffprobe | `IMediaProcessor` | `FfmpegMediaProcessor` |
| HttpClient (web pages) | `IPageFetcher` | `HttpPageFetcher` |
| SQLite + Dapper | `ILibraryRepository` | `SqliteLibraryRepository` |
| Tomlyn (TOML files) | `ISettingsStore`, `IPrefixStore`, `IStorageRegistry` | `TomlSettingsStore`, `TomlFile`, ... |
| rclone | `IStorageBackend`, `ISignInStorage` | `RcloneBackend`, `Rclone` |
| Credential Manager | `ICredentialStore` | `WindowsCredentialStore` |
| Processes | `IProcessRunner` (Infrastructure) | `ProcessRunner` |

Use cases (`MakeClipHandler`, `BuildEpisodeHandler`, ...) only see the interfaces. Data crosses the
boundary as our own **records** (`ResolvedVideo`, `CutInput`, `Clip`, ...), never library types.

## 5. Use cases

One class per job, in Core: `MakeClipHandler`, `BuildEpisodeHandler`, `ScrapeHandler`,
`UploadService`, `DoctorService`. Inputs are `…Request` records and outputs `…Result` records.
They report progress through `IReporter` and accept a `CancellationToken`.
Cheap checks come first, so nothing is downloaded for a request that can't succeed.

## 6. CLI commands (Spectre.Console.Cli)

- One folder per command group, mirroring the command tree: `Commands/Clips`, `Commands/Episode`, ...
- Each command is a **Settings** class (options, arguments, `Validate()`) and a thin **Command**
  class: settings → request → handler → exit code. No logic in commands.
- All settings inherit `GlobalSettings` (`--verbose`, `--quiet`).
- Validation that needs no I/O goes in `Settings.Validate()`, so it fails as a usage error (exit 2).
- Commands get their dependencies through the constructor (registered in `OuttaServices`).
- The command tree is defined once, in `OuttaApp.Configure`.
- Output that isn't a table goes through `SpectreReporter`; never `Console.WriteLine`.

## 7. Naming

| Thing | Rule | Example |
|---|---|---|
| Interface | `I` + role | `IVideoResolver` |
| Adapter | tool + role | `YtDlpVideoResolver`, `FfmpegMediaProcessor` |
| Use case | verb + noun + `Handler` | `MakeClipHandler` |
| Request / result | `…Request`, `…Result` | `MakeClipRequest` |
| Async method | `…Async` | `ResolveAsync` |
| Private field | `_camelCase` | `_reporter` |
| Constant / static readonly | `PascalCase` | `DefaultClipCap`, `ReservedWindowsNames` |
| Regex | `[GeneratedRegex]` method named `…Regex` | `VideoIdRegex()`, `PlayerIdRegex()` |
| Test class | `<ClassUnderTest>Tests` | `TimeRangeTests` |
| Test method | `Method_Scenario_ExpectedResult` | `Parse_EndBeforeStart_ThrowsInvalidTimeRange` |

Regexes are always source-generated and named, never inline strings:

```csharp
[GeneratedRegex(@"^(?:https?:)?//(?:www\.)?dai\.ly/([a-zA-Z0-9]{5,})", RegexOptions.IgnoreCase)]
private static partial Regex ShortUrlRegex();
```

`.editorconfig` enforces naming and style; `dotnet format --verify-no-changes` runs in CI, and the
build treats warnings as errors.

## 8. Tests

**Layout mirrors the source.** `src/OuttaContext.Core/Clips/MakeClipHandler.cs` is tested in
`tests/OuttaContext.Core.Tests/Clips/MakeClipHandlerTests.cs`.

**Arrange / Act / Assert.** One behaviour per test. Mark the sections with comments when a test has
more than a couple of lines:

```csharp
[Fact]
public async Task HandleAsync_RangeLongerThanCap_ThrowsClipTooLongWithoutNetwork()
{
    // Arrange
    var request = new MakeClipRequest(YouTubeUrl, "1:30-11:52", "Typo");

    // Act
    var error = await Should.ThrowAsync<ClipTooLongException>(() => _builder.Build().HandleAsync(request, ct));

    // Assert
    error.Hint.ShouldContain("max-clip-length");
    _builder.Resolver.Requests.ShouldBeEmpty();
}
```

**Tools:** xUnit v3 (`[Fact]`, `[Theory]` + `[InlineData]`), Shouldly for assertions.

**Fakes, not mocks.** `tests/OuttaContext.TestKit/Fakes` has small working fakes
(`FakeVideoResolver`, `FakeMediaProcessor`, `RecordingReporter`, `InMemoryLibraryRepository`, ...).
They record what they were asked to do and can be told to fail. Infrastructure tests use
`FakeProcessRunner` to check the exact command line of yt-dlp/ffmpeg/rclone without running them.

**Builders** keep setup short: `MakeClipHandlerBuilder`, `TempDirectory` (a folder deleted after the test),
`Fixtures.Read("hls", "fmp4_media.m3u8")`.

**Unit tests** never use the network, real processes or the user's data folder.
**Integration tests** (`[Trait("Category", "Integration")]`) generate videos with ffmpeg, serve them
from a local web server and run the real yt-dlp + ffmpeg pipeline. They skip themselves when the
tools aren't installed.

**CLI tests** run the real command tree through `CliHarness`, with only the outside world faked,
and check exit codes and printed output.

## 9. Secrets

- `ICredentialStore` (Windows Credential Manager) is the only place secrets live.
- Secrets never go into TOML, SQLite, logs or **command-line arguments** (other programs can see
  those). They reach rclone through environment variables (`ProcessSpec.Environment`).
- Logged ffmpeg commands hide `-headers` values (they can carry cookies).

## 10. Data compatibility

The data files (`settings.toml`, `prefixes.toml`, `storage.toml`, `library.db`) keep the format of the
earlier Python version, so existing clips and episodes keep working. Tests in
`Infrastructure.Tests/Persistence` read files written in that format. Don't rename keys or columns
without a migration.
