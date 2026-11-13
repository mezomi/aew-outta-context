# Conventions

How this codebase is organised and why. If you're new to the project (or to the libraries it uses),
start here.

## 1. The big picture

```
src/
  OuttaContext.Core/            What the app does: value objects, rules, interfaces, use cases.
                                Only CSharpFunctionalExtensions (Result, Maybe, ValueObject) and the DI abstractions.
  OuttaContext.Infrastructure/  How it talks to the outside world: yt-dlp, ffmpeg, rclone, SQLite, TOML, the keychain, HTTP.
  OuttaContext.Cli/             The `outta` command: Spectre.Console.Cli commands and the console output.
tests/
  OuttaContext.TestKit/         Shared fakes, builders and Result assertions.
  OuttaContext.Core.Tests/      Mirrors src/OuttaContext.Core folder by folder.
  OuttaContext.Infrastructure.Tests/
  OuttaContext.Cli.Tests/
  OuttaContext.IntegrationTests/ Real ffmpeg + yt-dlp against videos generated on the fly.
  fixtures/                     Saved web pages and HLS playlists used by tests.
agents/skills/                  Instructions for AI coding assistants (.NET, .NET 11, testing, xUnit).
```

Dependencies only point inward: **Cli → Core ← Infrastructure**. Core has no I/O, so every rule in
it can be tested without a network, a disk or a process.

## 2. Expected failures are results, bugs are exceptions

This is the most important rule in the codebase.

| Situation | Use | Example |
|---|---|---|
| Something that **can normally go wrong**: bad input, a site refusing, a missing file, a name already taken | Return `Result<T, Error>` (or `UnitResult<Error>` when there is no value) | `ClipTimeSpan.Parse("2-1")` → failure `range.end_before_start` |
| Something that **may simply not be there** | Return `Maybe<T>` | `repository.FindClip(id)`, `tools.Find("rclone")` |
| A **bug**, or a state the program can't continue from | Throw | `VideoTime.FromTrusted(-1)`, a corrupt `settings.toml` (`ErrorException`) |

Never use `null` to mean "not there" in our own types: use `Maybe<T>`. (Library-facing code such as
Spectre option properties may still use `string?`.)

### `Error`

```csharp
public sealed record Error(string Code, string Message, string Hint = "", string? Details = null);
```

- **Code**: stable, for tests and logs: `clip.too_long`, `yt-dlp.forbidden`, `storage.not_found`.
- **Message**: what went wrong, in plain words. **Hint**: how the user fixes it.
- **Details**: the outside program's own output, printed only with `--verbose`.

Errors are created in **one static class per area**, so every message lives in one place:
`TimeErrors`, `ClipErrors`, `EpisodeErrors`, `StorageErrors`, `SettingsErrors`, `ScrapeErrors`,
`MediaErrors` (Core) and `YtDlpErrors`, `FfmpegErrors`, `StorageBackendErrors`, `UpdateErrors`, `WebErrors`
(Infrastructure). `YtDlpErrors.Translate` turns yt-dlp's raw text into an `Error` with a table of
rules (first match wins). To handle a new yt-dlp message, add a row there; nothing else changes.

### Railway style (CSharpFunctionalExtensions)

A use case is a chain of steps. Each step returns a `Result`; the first failure skips the rest and
is returned. Read it top to bottom like a recipe:

```csharp
public Task<Result<MakeClipResult, Error>> HandleAsync(MakeClipRequest request, CancellationToken ct) =>
    Check(request)                                   // title, range, clip cap, link, episode: no network
        .Bind(plan => ResolveAsync(plan, ct))        // ask the site (tries each request variant)
        .Bind(plan => DownloadAsync(plan, ct))       // fetch only the range, cut it exactly
        .Map(SaveClip)                               // can't fail: just transforms the value
        .Bind(clip => AddToEpisode(clip, request))
        .Bind(result => UploadAsync(result, request, ct));
```

| Method | Use it when the next step… |
|---|---|
| `Map(x => y)` | can't fail: transform the value |
| `Bind(x => Result<…>)` | can fail |
| `Ensure(x => bool, error)` | is a yes/no rule |
| `Tap(x => …)` | is a side effect (print, save) that keeps the value |
| `Check(x => UnitResult)` | can fail but produces no new value |
| `MapError(e => …)` | should replace the error with a clearer one |

When a loop or early exit reads more clearly than a chain, write a plain method that checks
`result.IsFailure` and returns `result.Error`. Clarity beats cleverness.

### Small helpers (`Core/Common/ResultExtensions`)

- `DropValue()`: `Result<T, Error>` → `UnitResult<Error>`.
- `ValueOrNone()`: `Result<T, Error>` → `Maybe<T>`, when the reason doesn't matter.
- `ValueOrThrow()`: only where a failure means "can't continue" (it throws `ErrorException`).

## 3. Value objects: types that enforce their own rules

A value with rules gets its own type, so an invalid value can't exist past the point it was read.
Each has a factory that returns a `Result` (for user input) and, where the app builds values itself,
a `FromTrusted` that throws (a bad value there is a bug).

| Type | Rule | Made with |
|---|---|---|
| `VideoTime` | seconds, never negative; reads `90`, `1:30`, `1:02:10` | `Parse`, `FromSeconds`, `FromTrusted` |
| `ClipTimeSpan` | start and end `VideoTime`, end after start; `FitsIn(videoLength)` | `Parse("1:30-1:52")`, `Create` |
| `ClipTitle` / `EpisodeName` | single-spaced, non-empty, has a Windows-safe `FileName` | `Create` |
| `StorageName` | letters, numbers, `-`, `_`, up to 32 | `Create` |
| `StorageProvider` | one of gdrive, onedrive, dropbox, b2, local (`EnumValueObject`) | `Parse` |
| `VideoQuality`, `BrowserName` | one of the supported values | `Parse` |
| `LinkPrefix`, `WebUrl` | starts with http(s):// | `Create` |
| `ByteRange` | `First ≤ Last`, not negative (HLS pieces) | `FromLength` |
| `ByteSize` | bytes, not negative; prints `12.3 MB` | `FromBytes`, `OfFile` |
| `RequestHeaders` | immutable, compared by content | `From`, `RefererFor`, `None` |

Value objects inherit `SimpleValueObject<T>`, `ValueObject`, `ComparableValueObject` or
`EnumValueObject<T>` from CSharpFunctionalExtensions, so two with the same contents are equal.
Records (`Clip`, `Episode`, `ResolvedVideo`, ...) hold value objects, not raw `double`/`string`.

Persistence code converts at the edge: `SqliteLibraryRepository` and `TomlSettingsStore` read raw
columns/keys and build value objects; the file formats don't change (section 12).

## 4. Every library sits behind our own interface

| Library / program | Our interface (Core) | Adapter (Infrastructure / Cli) |
|---|---|---|
| Spectre.Console | `IReporter`, `IProgressTask` | `SpectreReporter`, `LiveProgressTask` |
| yt-dlp | `IVideoResolver`, `IClipDownloader` | `YtDlpVideoResolver`, `YtDlpStreamFetcher`, `ClipDownloader` |
| ffmpeg / ffprobe | `IMediaProcessor` | `FfmpegMediaProcessor` |
| HttpClient (web pages) | `IPageFetcher` | `HttpPageFetcher` (typed client) |
| SQLite + Dapper | `ILibraryRepository` | `SqliteLibraryRepository` |
| Tomlyn (TOML files) | `ISettingsStore`, `IPrefixStore`, `IStorageRegistry` | `TomlSettingsStore`, `TomlPrefixStore`, `TomlStorageRegistry` |
| rclone | `IStorageBackend`, `ISignInStorage`, `IStorageBackendFactory` | `RcloneBackend`, `LocalFolderBackend`, `StorageBackendFactory` |
| Credential Manager | `ICredentialStore` | `WindowsCredentialStore` |
| Processes | `IProcessRunner` (Infrastructure) | `ProcessRunner` |

Use cases (`MakeClipHandler`, `BuildEpisodeHandler`, ...) only see the interfaces. Data crosses the
boundary as our own records and value objects, never library types.

## 5. Dependency injection (Microsoft.Extensions.DependencyInjection)

Each project registers its own services with one extension method:

```csharp
new ServiceCollection()
    .AddOuttaInfrastructure(paths)   // adapters, settings, HttpClient factory
    .AddOuttaCore()                  // use cases and domain services
    .AddOuttaCli(console);           // console and the one shared SpectreReporter
```

- Classes take what they need in the **constructor**; no service locators, no `Func<>` callbacks
  for logging (inject `IReporter`).
- Almost everything is a singleton: one command runs per process.
- HTTP adapters are **typed clients** (`AddHttpClient<IPageFetcher, HttpPageFetcher>`).
- Spectre.Console.Cli creates commands through `TypeRegistrar`, which wraps the same container.
- `OuttaServicesTests` builds the container with `ValidateOnBuild` and resolves every command, so a
  missing registration fails a test, not a user.

## 6. Use cases

One class per job, in Core: `MakeClipHandler`, `BuildEpisodeHandler`, `ScrapeHandler`,
`UploadService`, `EpisodeService`, `DoctorService`. Inputs are `…Request` records (raw user text);
outputs are `Result<…Result, Error>`. They report progress through `IReporter` and accept a
`CancellationToken`. Cheap checks come first, so nothing is downloaded for a request that can't succeed.

## 7. CLI commands (Spectre.Console.Cli)

- One folder per command group, mirroring the command tree: `Commands/Clips`, `Commands/Episode`, ...
- Each command is a **Settings** class (options, arguments, `Validate()`) and a thin **Command**
  class, **each in its own file**: settings → request → use case → exit code. No logic in commands.
- All settings inherit `GlobalSettings` (`--verbose`, `--quiet`).
- Validation that needs no I/O goes in `Settings.Validate()`, so it fails as a usage error (exit 2).
- A command ends with `.ToExitCode(reporter, onSuccess)` (`Commands/CommandResults.cs`): on success it
  runs `onSuccess` and returns 0; on failure it prints `error:`/`hint:` and returns 1.
- `ErrorHandler` only sees exceptions: usage errors (2), Ctrl+C (130), `ErrorException` and bugs (1).
- The command tree is defined once, in `OuttaApp.Configure`.
- Output that isn't a table goes through `SpectreReporter`; never `Console.WriteLine`.

```
error: 1:30-11:52 is 10:22 long, over the 45s clip cap.
hint: Check the range for a typo. ...
```

## 8. One type per file

Each class, record, interface or enum gets its own file named after it. Keep a type in another
file only when it is **private to it** (a nested helper record such as `MakeClipHandler.ClipPlan`,
or Dapper row classes in `SqliteLibraryRepository`).

## 9. Naming

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
| Error factory | area + `Errors` | `ClipErrors.TooLong(...)` |
| Error code | `area.reason` | `clip.too_long`, `yt-dlp.network` |
| Test class | `<ClassUnderTest>Tests` | `ClipTimeSpanTests` |
| Test method | `Method_Scenario_ExpectedResult` | `Parse_EndBeforeStart_FailsWithHint` |

Regexes are always source-generated and named, never inline strings:

```csharp
[GeneratedRegex(@"^(?:https?:)?//(?:www\.)?dai\.ly/([a-zA-Z0-9]{5,})", RegexOptions.IgnoreCase)]
private static partial Regex ShortUrlRegex();
```

`.editorconfig` enforces naming and style; `dotnet format --verify-no-changes` runs in CI, and the
build treats warnings as errors.

## 10. Tests

**Layout mirrors the source.** `src/OuttaContext.Core/Clips/MakeClipHandler.cs` is tested in
`tests/OuttaContext.Core.Tests/Clips/MakeClipHandlerTests.cs`.

**Arrange / Act / Assert.** One behaviour per test. Mark the sections with comments when a test has
more than a couple of lines:

```csharp
[Fact]
public async Task HandleAsync_RangeLongerThanCap_FailsWithClipTooLongWithoutNetwork()
{
    // Arrange
    var request = new MakeClipRequest(YouTubeUrl, "1:30-11:52", "Typo");

    // Act
    var result = await _builder.Build().HandleAsync(request, TestContext.Current.CancellationToken);

    // Assert
    result.ShouldFailWith("clip.too_long").Hint.ShouldContain("max-clip-length");
    _builder.Resolver.Requests.ShouldBeEmpty();
}
```

**Tools:** xUnit v3 (`[Fact]`, `[Theory]` + `[InlineData]`), Shouldly for assertions.

**Results:** `TestKit/Assertions/ResultAssertions` adds `ShouldSucceed()` (returns the value),
`ShouldFailWith("code")` (returns the `Error`) and `ShouldHaveValue()` for `Maybe`. Assert on the
error **code**, and on message/hint text only when the wording is the point of the test.
`TestKit/Builders/Values` makes value objects briefly: `Values.Span("1:30-1:52")`, `Values.Time(90)`.

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

## 11. Secrets

- `ICredentialStore` (Windows Credential Manager) is the only place secrets live.
- Secrets never go into TOML, SQLite, logs or **command-line arguments** (other programs can see
  those). They reach rclone through environment variables (`ProcessSpec.Environment`).
- Logged ffmpeg commands hide `-headers` values (they can carry cookies).

## 12. Data compatibility

The data files (`settings.toml`, `prefixes.toml`, `storage.toml`, `library.db`) keep the format of the
earlier Python version, so existing clips and episodes keep working. Tests in
`Infrastructure.Tests/Persistence` read files written in that format. Don't rename keys or columns
without a migration.
