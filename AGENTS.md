# Working on outta-context (for AI assistants)

1. Read [`CONVENTIONS.md`](CONVENTIONS.md) first. It is the source of truth for structure, naming and tests.
2. Skills for .NET, .NET 11, testing and xUnit are in [`agents/skills/`](agents/skills/). Use them for
   technique; where they disagree with CONVENTIONS.md, follow CONVENTIONS.md.
3. Before finishing, run:
   ```
   dotnet format --verify-no-changes
   dotnet build -c Release
   dotnet test --filter-not-trait "Category=Integration" --ignore-exit-code 8
   ```
   Integration tests need ffmpeg and yt-dlp on PATH:
   `dotnet test --project tests/OuttaContext.IntegrationTests`.
4. Never put secrets in files, logs or command-line arguments (see "Secrets" in CONVENTIONS.md).
