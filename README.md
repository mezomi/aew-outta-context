# outta-context

`outta` is a Windows command-line tool for making the **AEW Outta Context** series. It finds videos
on web pages, cuts titled clips from Dailymotion or YouTube, collects them into ~10-minute
episodes, builds each episode into one MP4, and uploads clips and episodes to cloud storage.

```
scrape a page → clip a time range with a title → the clip joins the current episode
→ at 10:00, build the episode → upload → the next episode opens
```

Written in C# / .NET 10 with Spectre.Console, Microsoft dependency injection and
CSharpFunctionalExtensions. It runs as a single `outta.exe`, with no .NET install needed.

---

## Install

Open PowerShell and paste:

```powershell
irm https://github.com/mezomi/aew-outta-context/releases/latest/download/install.ps1 | iex
```

This downloads `outta.exe` to `%LOCALAPPDATA%\Programs\outta`, adds it to your PATH and downloads
yt-dlp. Then install ffmpeg, which is required:

```powershell
winget install ffmpeg
```

Open a **new** terminal and check everything:

```powershell
outta doctor
```

**To update** later (outta.exe and yt-dlp):

```powershell
outta update
```

Your clips, episodes and settings are kept, because they live outside the exe.

Optional programs:

| Program | Needed for | Install |
|---|---|---|
| Deno | some YouTube videos | `winget install DenoLand.Deno` |
| rclone | uploading to Google Drive / OneDrive / Dropbox / B2 | `winget install Rclone.Rclone` |

### Why yt-dlp and ffmpeg?

- **yt-dlp** turns a video page link into the real stream addresses. Sites change how that works
  all the time, and yt-dlp is updated for it constantly. It also lets outta pose as Chrome/Firefox,
  which Dailymotion requires. outta downloads and updates it for you.
- **ffmpeg** cuts the exact range and builds episodes (converting clips to one size and frame
  rate, then joining them).

---

## Quick start

```powershell
outta config set-destination "C:\Clips"
outta scrape dailymotion "https://example.com/article"
outta clip "https://geo.dailymotion.com/player/xc394.html?video=k1ojSwOaH7psOtMbpGS" --range 45:30-45:38 --title "Steve the Clown"
outta clip "https://geo.dailymotion.com/player/xc394.html?video=k1ojSwOaH7psOtMbpGS" --range 45:29-45:38 --title "Steve the Clown" --replace
outta episode status
outta episode build
```

```
→ Detected source: Dailymotion
→ Reading video info...
→ Video: "AEW Dynamite" (52:14)
→ Clipping 45:30-45:38 (8s, best up to 1080p)
→ Fetching the stream pieces for this range
  ⣾ ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100% 3/3 pieces 00:00:02
→ Cutting the exact range
  ⣾ ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100% 8.0s of 8.0s 00:00:01
✓ Saved C:\Clips\Steve the Clown.mp4 (clip 1, 8s)
✓ Added to "Episode 1": 0:08 of 10:00 (9:52 left)
```

---

## Commands

Every command has `--help`. Add `-v` (`--verbose`) for details or `-q` (`--quiet`) for results only.

### `outta clip URL --range START-END --title TITLE`

- **`--title` / `-t` is required.** It becomes the file name; a title that's already used is refused
  unless you add `--replace`.
- **`--replace`** replaces the clip that already has this title (e.g. to fix its range).
  **`--replace ID`** replaces clip ID, and can give it a new title. Either way the clip keeps its ID
  and its place in its episode, the old file is deleted, and nothing changes if the new download fails.
  If that episode was already built, rebuild it with `outta episode build "Episode N"`.
- **`--range` / `-r` is required:** `90-112`, `1:30-1:52` or `1:02:10-1:02:30`.
- **Clip cap:** ranges over **45 seconds** are refused before anything downloads, so a typo like
  `1:30-11:52` is caught. Change it with `outta config set max-clip-length 1:30`, or turn it off with `off`.
- **Length check:** the range is checked against the video's real length before downloading.
- `--episode NAME` adds the clip to a named episode; `--no-episode` adds it to none.
- `--upload` (and `--to NAME`) uploads it afterwards.
- `--referer PAGE_URL` is for embed-only videos. Pages found with `outta scrape` are remembered automatically.
- `--cookies-from-browser firefox` / `--cookies FILE` use your browser's login session.

Accepted links: YouTube (`watch`, `youtu.be`, `shorts`, `embed`, `live`), Dailymotion (`video`,
`embed`, `geo.dailymotion.com/player`, `dai.ly`), and direct `.m3u8` / `.mp4` / `.mpd` stream links.

### If Dailymotion refuses a video (HTTP 403)

outta already asks the way Dailymotion's embedded player does, and poses as Chrome and Firefox. If
it still fails:

1. **Use your browser's session.** Close Firefox, then add `--cookies-from-browser firefox`, or set it
   once with `outta config set cookies-from-browser firefox`.
2. **Clip the stream link from your browser.** Press **F12**, open the **Network** tab, filter on
   `m3u8`, play the video, then right-click the first `.m3u8` line → **Copy → Copy URL**. Run this right away:
   ```powershell
   outta clip "PASTE_THE_M3U8_LINK" --range 45:30-45:38 --title "Steve the Clown"
   ```

### `outta scrape`

```powershell
outta scrape dailymotion PAGE_URL [--json]
outta scrape youtube PAGE_URL [--json]
outta scrape prefixes add dailymotion "https://geo.dailymotion.com/player/"
outta scrape prefixes list [SITE]
outta scrape prefixes remove SITE PREFIX
```

### `outta episode`

```powershell
outta episode status [NAME] [--json]
outta episode list [--json]
outta episode new "All Out special"
outta episode move CLIP_ID POSITION
outta episode remove CLIP_ID              # the file is kept
outta episode build [NAME] [--upload] [--to NAME] [--clean-local]
```

`build` converts every clip to 1920x1080, 30 fps, H.264 with AAC audio (letterboxed, with silent
audio added where needed), joins them into `<clip folder>\Episodes\<episode>.mp4`, and opens
"Episode N+1". Converted clips are cached, so a rebuild after reordering is quick.

### `outta storage` and `outta upload`

```powershell
outta storage add drive --provider gdrive --folder "AEW Outta Context"
outta storage add backup --provider local --folder "D:\AEW backup"
outta storage add archive --provider b2 --folder "my-bucket/aew" --account KEY_ID
outta storage list | test NAME | default NAME | remove NAME
outta upload "C:\Clips\Table spot.mp4" | outta upload 7 | outta upload "Episode 3" --to backup
```

Google Drive, OneDrive and Dropbox open your browser to sign in; outta never sees your password.
Logins are kept in an rclone config encrypted with a password stored in **Windows Credential
Manager**. The Backblaze key is stored there too. `storage.toml` holds only names, providers and folders.

### `outta config`, `outta doctor`, `outta update`

```powershell
outta config show
outta config set-destination FOLDER
outta config set episode-length 10:00
outta config set max-clip-length 45          # or 1:30, or off
outta config set max-quality 1080            # 360 480 720 1080 1440 2160
outta config set cookies-from-browser firefox
outta doctor [--no-storage]
outta update [--check] [--yt-dlp-only]
```

## Where data lives

`%LOCALAPPDATA%\outta-context\` (`outta config show` prints it; set `OUTTA_HOME` to use another folder):
`settings.toml`, `prefixes.toml`, `storage.toml`, `library.db` (clips, episodes, uploads),
`rclone.conf` (encrypted), `cache\` (converted clips) and `bin\` (yt-dlp).

---

## Development

Needs the .NET 10 SDK. Integration tests also need ffmpeg and yt-dlp on PATH.

```bash
dotnet build
dotnet test --filter-not-trait "Category=Integration" --ignore-exit-code 8   # unit tests, ~3s
dotnet test --project tests/OuttaContext.IntegrationTests                    # real ffmpeg + yt-dlp
dotnet format --verify-no-changes                                            # style and naming
dotnet run --project src/OuttaContext.Cli -- clip --help                     # run without publishing
dotnet publish src/OuttaContext.Cli -c Release -r win-x64 -o dist            # single-file outta.exe
```

- [`CONVENTIONS.md`](CONVENTIONS.md): structure, results vs exceptions, value objects, dependency
  injection, naming, CLI and test conventions.
- [`AGENTS.md`](AGENTS.md) and [`agents/`](agents/): instructions and skills for AI coding assistants.
- CI runs on Windows and Linux, builds `outta.exe`, checks it can download yt-dlp and pose as a
  browser, and publishes it to the latest release.

## Notes

- YouTube's terms don't allow downloading except through its own features, and AEW footage is
  copyrighted. Short, transformative clips may be fair use, but uploads may still be claimed. Keep
  the tool for your own editing workflow.
- Sites change often. If downloads start failing, run `outta update` first.
