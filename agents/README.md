# agents/

Instructions for AI coding assistants (Claude Code, GitHub Copilot, Codex, ...) working on this repo.

| Folder | What it is | Source |
|---|---|---|
| `skills/dotnet/` | General .NET + C#: csharp-expert, csharp-refactoring, msbuild, breaking changes, local SDK setup | [dotnet/skills](https://github.com/dotnet/skills) `plugins/dotnet` (MIT, .NET Foundation) |
| `skills/dotnet11/` | .NET 11 APIs: System.Text.Json and Process changes | [dotnet/skills](https://github.com/dotnet/skills) `plugins/dotnet11` (MIT) |
| `skills/dotnet-test/` | Writing and analysing tests: test smells, coverage, testability, filters | [dotnet/skills](https://github.com/dotnet/skills) `plugins/dotnet-test` (MIT) |
| `skills/csharp-xunit/` | xUnit best practices, data-driven tests | [github/awesome-copilot](https://github.com/github/awesome-copilot) `skills/csharp-xunit` (MIT) |

The project's own rules are in [`/CONVENTIONS.md`](../CONVENTIONS.md). Where a skill disagrees with
them (for example MSTest examples, or `Assert.Equal` instead of Shouldly), **CONVENTIONS.md wins**.

Each skill is a folder with a `SKILL.md`. Assistants read the `description` at the top to decide when to use it.
To update a skill, copy the newer folder from its source repo over the old one.
