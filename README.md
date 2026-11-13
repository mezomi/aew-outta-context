# outta-context

`outta` is a command-line tool for making the **AEW Outta Context** series. It finds video links on web pages, cuts titled clips from Dailymotion or YouTube, collects them into episodes of about 10 minutes, builds each finished episode into one MP4, and uploads clips and episodes to cloud storage.

```
scrape a page → clip a time range with a title → clip joins the current episode
→ at 10:00, build the episode → upload → the next episode opens
```

---

## Install (no source code needed)

Pick **one** of these. Neither one clones the repository onto your computer.

### Option A: one-line install of `outta.exe` (no Python needed)

Open PowerShell and paste:

```powershell
irm https://github.com/mezomi/aew-outta-context/releases/latest/download/install.ps1 | iex
```

It downloads the latest `outta.exe` to `%LOCALAPPDATA%\Programs\outta`, adds that folder to your PATH, and tells you if ffmpeg is missing. Then open a **new** terminal and run `outta doctor`.

**To update**, run the same line again. Your clips, episodes and settings are kept, because they live outside the exe.

If you prefer to do it by hand, download `outta.exe` from the **Releases** page, put it in a folder such as `C:\Tools`, and add that folder to your PATH.

### Option B: install the command with `uv` (gets updates easily)

This puts only the `outta` command on your PATH, in its own isolated environment. Nothing else is installed and nothing is cloned.

```powershell
winget install astral-sh.uv
uv tool install git+https://github.com/mezomi/aew-outta-context
```

While this work is still on its branch and not yet merged into `main`, install from the branch:

```powershell
uv tool install git+https://github.com/mezomi/aew-outta-context@claude/outta-context-cli
```

To update (this also updates yt-dlp, which sites break often):

```powershell
uv tool upgrade outta-context
```

To uninstall:

```powershell
uv tool uninstall outta-context
```

`pipx install git+https://github.com/mezomi/aew-outta-context` also works if you already use pipx.

### Outside programs

| Program | Needed for | Install (Windows) |
|---|---|---|
| **ffmpeg** | cutting clips, building episodes (**required**) | `winget install ffmpeg` |
| **Deno** | some YouTube videos (yt-dlp uses it to read YouTube's player) | `winget install DenoLand.Deno` |
| **rclone** | uploading to Google Drive / OneDrive / Dropbox / B2 | `winget install Rclone.Rclone` |

Open a new terminal after installing, then run:

```powershell
outta doctor
```

It checks each of these and prints the fix for anything missing.

---

## Quick start

```powershell
# 1. Where clips go (built episodes go in an "Episodes" subfolder)
outta config set-destination "C:\Clips"

# 2. Find videos on an article page (nothing is downloaded)
outta scrape dailymotion "https://example.com/article"

# 3. Cut a clip. --title and --range are both required.
outta clip "https://www.dailymotion.com/video/k1ojSwOaH7psOtMbpGS" --range 1:30-1:52 --title "He said what"
outta clip "https://youtu.be/dQw4w9WgXcQ" --range 0:45-1:05 --title "Table spot"

# 4. See how full the episode is
outta episode status

# 5. When it reaches 10:00, build it (and optionally upload)
outta episode build
```

Example output:

```
→ Detected source: YouTube
→ Reading video info...
→ Video: "AEW Dynamite highlights" (14:32)
→ Downloading 0:45-1:05 (20s, best up to 1080p, exact cuts)
  ████████████████████ 100%  8.4/8.4 MB
✓ Saved C:\Clips\Table spot.mp4 (clip 7, 20s)
✓ Added to "Episode 3": 7:41 of 10:00 (2:19 left)
```

---

## Commands

Global options: `--verbose`/`-v` (details, the ffmpeg/yt-dlp commands, and tracebacks), `--quiet`/`-q` (results and errors only), `--version`. Every command has `--help`.

### `outta clip URL --range START-END --title TITLE`

Downloads one time range of a Dailymotion or YouTube video as `<title>.mp4` and adds it to the current episode.

- **`--title` / `-t` is required.** It becomes the file name, characters Windows can't use are replaced with `_`, and a title that's already used is refused (the check ignores case).
- **`--range` / `-r` is required**: `90-112`, `1:30-1:52` or `1:02:10-1:02:30`.
- **Clip cap.** Ranges longer than **45 seconds** are refused before anything downloads, so a typo like `1:30-11:52` (meant `1:30-1:52`) is caught. Change the cap with `outta config set max-clip-length 1:30`, or turn it off with `outta config set max-clip-length off`.
- **Length check.** The range is checked against the video's real length before downloading. A start or end past the end of the video is refused. If the site doesn't report the length, you get a warning.
- `--episode NAME` adds the clip to a named episode. `--no-episode` doesn't add it to any.
- `--upload` uploads it to the default storage afterwards, and `--to NAME` picks another storage.
- `--referer PAGE_URL` is for embed-only videos. If you found the video with `outta scrape`, the page is remembered and sent for you.
- A clip that takes the episode past 10:00 is still added, and you're told the episode is ready to build.

Accepted link forms: `youtube.com/watch?v=`, `youtu.be/`, `/shorts/`, `/embed/`, `/live/`, `dailymotion.com/video/`, `/embed/video/`, `geo.dailymotion.com/player…?video=`, `dai.ly/`.

### `outta scrape`

```powershell
outta scrape dailymotion PAGE_URL [--json]
outta scrape youtube PAGE_URL [--json]
outta scrape prefixes add dailymotion "https://geo.dailymotion.com/player/"
outta scrape prefixes list [SITE]
outta scrape prefixes remove SITE PREFIX
```

Lists the videos embedded or linked on a page, in page order, without duplicates. If a site uses a new embed style, register its link prefix: any link starting with it counts as a video, and the ID is taken from its `video=` parameter or the last part of its path. If nothing is found, the page probably loads its player with JavaScript. In that case, copy the link from your browser instead.

### `outta episode`

```powershell
outta episode status [NAME] [--json]   # clips in order, total, time left
outta episode list [--json]            # all episodes
outta episode new "All Out special"    # start one by hand; new clips go into it
outta episode move CLIP_ID POSITION    # reorder
outta episode remove CLIP_ID           # take a clip out (file is kept)
outta episode build [NAME] [--upload] [--to NAME] [--clean-local]
```

`build` converts every clip to 1920x1080, 30 fps, H.264 with AAC 48 kHz audio. Clips are scaled and padded with black bars so nothing is stretched, and clips without audio get a silent track. The clips are then joined into `<clip folder>\Episodes\<episode>.mp4`. Converted copies are cached, so rebuilding after a reorder is fast. After a build, the episode is marked built and the next one, "Episode N+1", opens. `--clean-local` (with `--upload`) deletes the local clip files once they're uploaded.

### `outta storage` and `outta upload`

```powershell
outta storage add drive --provider gdrive --folder "AEW Outta Context"
outta storage add backup --provider local --folder "D:\AEW backup"
outta storage add archive --provider b2 --folder "my-bucket/aew" --account KEY_ID
outta storage list | test NAME | default NAME | remove NAME

outta upload "C:\Clips\Table spot.mp4"   # a file
outta upload 7                           # a clip, by ID
outta upload "Episode 3" --to backup     # a built episode and its clips
```

Providers: `gdrive`, `onedrive`, `dropbox`, `b2` (all through rclone) and `local` (another folder, an external drive or a NAS). The first storage you add becomes the default. On a cloud storage, an episode upload creates `AEW Outta Context/Episode 3/Episode 3.mp4`, with its clips in `Episode 3/clips/`. Files that were already uploaded unchanged are skipped.

### `outta config` and `outta doctor`

```powershell
outta config show
outta config set-destination FOLDER
outta config set episode-length 10:00
outta config set max-clip-length 45      # or 1:30, or off
outta config set max-quality 1080        # 360 480 720 1080 1440 2160
outta doctor [--no-storage]
```

`doctor` checks ffmpeg, the yt-dlp version (it warns when yt-dlp is more than about a month old), a JS runtime for YouTube, rclone, the clip folder, the system keychain and each storage login. It prints the fix for anything that fails.

---

## Passwords and logins

- The tool never asks for your Google, Microsoft or Dropbox password. `outta storage add` opens your browser so you can sign in and approve access. rclone keeps the resulting token in the tool's own `rclone.conf`.
- That `rclone.conf` is encrypted with a random password. The password is stored in **Windows Credential Manager** and handed to rclone through an environment variable when it runs. It never sits in a file or on a command line.
- The Backblaze key is asked for with hidden input and stored in Credential Manager. It only reaches rclone through environment variables.
- `storage.toml` holds only names, providers and folders. `outta storage remove NAME` deletes the login and the stored key.
- To revoke access completely, remove the app from your Google account's third-party access page. `outta doctor` will then show that login as failed.

## Where data lives

The data folder is `%LOCALAPPDATA%\outta-context\` (`outta config show` prints it). Set `OUTTA_HOME` to use another folder, for example for a portable setup.

| File | Holds |
|---|---|
| `settings.toml` | clip folder, episode length, clip cap, max quality, default storage (safe to edit by hand) |
| `prefixes.toml` | registered scrape prefixes |
| `library.db` | clips, episodes, order, upload history, scraped page links (SQLite) |
| `storage.toml` | storage names, providers, folders (never secrets) |
| `rclone.conf` | encrypted rclone logins |
| `cache\` | converted clip copies used when building episodes |

---

## Development

```bash
git clone https://github.com/mezomi/aew-outta-context && cd aew-outta-context
python -m venv .venv && . .venv/bin/activate      # Windows: .venv\Scripts\activate
pip install -e ".[dev]"
pytest                            # fast unit tests (no network, no ffmpeg)
pytest -m integration             # real ffmpeg + yt-dlp on generated test videos
pytest -m network --run-network   # real YouTube/Dailymotion clips (needs internet)
ruff check src tests && ruff format --check src tests && mypy src
python packaging/build_exe.py     # builds dist/outta(.exe)
```

Layout: `cli/` only reads options and prints. `workflows/` runs the steps of each job. `sources/`, `scraping/`, `media/`, `library/`, `storage/` and `settings/` each do one job. Only `PageFetcher`, `ClipDownloader`, `Ffmpeg`, `Rclone` and `CredentialStore` touch the network, other programs or the keychain. To add a site or provider, add one class and one line in its registry; `outta scrape <site>` commands are generated from the scraper registry.

## Notes

- YouTube's terms don't allow downloading except through its own features, and AEW footage is copyrighted. Short, transformative clips may be fair use, but that's a judgment call, and uploads may still be claimed by Content ID. Use the tool for your own editing workflow.
- yt-dlp needs frequent updates to keep up with site changes. If downloads start failing, update first (Option A: new exe; Option B: `uv tool upgrade outta-context`).
