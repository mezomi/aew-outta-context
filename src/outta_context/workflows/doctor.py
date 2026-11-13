"""outta doctor: check the outside programs and storage logins, and say how to fix each."""

from __future__ import annotations

import os
import shutil
import sys
import tempfile
from collections.abc import Callable
from dataclasses import dataclass
from datetime import date

from outta_context.core.errors import OuttaError
from outta_context.media.ffmpeg import Ffmpeg
from outta_context.settings.models import Settings
from outta_context.storage.credentials import CredentialStore
from outta_context.storage.registry import StorageRegistry

OK, WARN, FAIL = "ok", "warn", "fail"
YTDLP_MAX_AGE_DAYS = 31


@dataclass(frozen=True)
class Check:
    name: str
    state: str
    message: str
    fix: str = ""


def ytdlp_age_days(version: str, today: date | None = None) -> int | None:
    try:
        year, month, day = (int(p) for p in version.split(".")[:3])
        return ((today or date.today()) - date(year, month, day)).days
    except (ValueError, TypeError):
        return None


def update_hint() -> str:
    if getattr(sys, "frozen", False):
        return "Download the latest outta.exe from the project's Releases page."
    return "Run `uv tool upgrade outta-context` (or `pipx upgrade outta-context`)."


class Doctor:
    def __init__(
        self,
        ffmpeg: Ffmpeg,
        settings: Settings,
        storages: StorageRegistry,
        credentials: CredentialStore,
        which: Callable[[str], str | None] = shutil.which,
        ytdlp_version: Callable[[], str | None] | None = None,
    ) -> None:
        self.ffmpeg = ffmpeg
        self.settings = settings
        self.storages = storages
        self.credentials = credentials
        self.which = which
        self._ytdlp_version = ytdlp_version or _installed_ytdlp

    def run(self, test_storage: bool = True) -> list[Check]:
        checks = [self.check_python(), self.check_ffmpeg(), self.check_ytdlp(), self.check_js()]
        checks.append(self.check_clip_folder())
        checks.append(self.check_keyring())
        configs = self.storages.list()
        if any(c.provider != "local" for c in configs) or not configs:
            checks.append(self.check_rclone(required=any(c.provider != "local" for c in configs)))
        if test_storage:
            for config in configs:
                try:
                    self.storages.backend(config).test()
                    checks.append(Check(f"storage {config.name}", OK, f"{config.provider} works"))
                except OuttaError as exc:
                    checks.append(Check(f"storage {config.name}", FAIL, exc.message, exc.hint))
        return checks

    def check_python(self) -> Check:
        v = sys.version_info
        return Check("python", OK, f"{v.major}.{v.minor}.{v.micro}")

    def check_ffmpeg(self) -> Check:
        if not self.ffmpeg.available():
            return Check(
                "ffmpeg",
                FAIL,
                "ffmpeg/ffprobe not found",
                "Install it: `winget install ffmpeg`, then open a new terminal.",
            )
        version = self.ffmpeg.version() or "found"
        version = version.replace("ffmpeg version ", "").split(" Copyright")[0]
        return Check("ffmpeg", OK, version)

    def check_ytdlp(self) -> Check:
        version = self._ytdlp_version()
        if version is None:
            return Check("yt-dlp", FAIL, "yt-dlp is not installed", update_hint())
        age = ytdlp_age_days(version)
        if age is not None and age > YTDLP_MAX_AGE_DAYS:
            return Check(
                "yt-dlp", WARN, f"{version} is {age} days old; sites change often", update_hint()
            )
        return Check("yt-dlp", OK, version)

    def check_js(self) -> Check:
        for exe in ("deno", "node", "bun"):
            if self.which(exe):
                return Check("js runtime", OK, f"{exe} (used by yt-dlp for YouTube)")
        return Check(
            "js runtime",
            WARN,
            "no deno/node found; some YouTube videos may fail",
            "Install Deno: `winget install DenoLand.Deno`.",
        )

    def check_rclone(self, required: bool) -> Check:
        if self.which("rclone"):
            return Check("rclone", OK, "found")
        state = FAIL if required else WARN
        return Check(
            "rclone",
            state,
            "rclone not found (needed for Drive/OneDrive/Dropbox/B2)",
            "Install it: `winget install Rclone.Rclone`, then open a new terminal.",
        )

    def check_clip_folder(self) -> Check:
        folder = self.settings.clip_folder
        try:
            folder.mkdir(parents=True, exist_ok=True)
            with tempfile.NamedTemporaryFile(dir=folder, prefix=".outta-", delete=True):
                pass
        except OSError as exc:
            return Check(
                "clip folder",
                FAIL,
                f"{folder}: {exc}",
                "Pick another folder: `outta config set-destination FOLDER`.",
            )
        free = shutil.disk_usage(folder).free / 1e9
        state = WARN if free < 2 else OK
        return Check("clip folder", state, f"{folder} ({free:.1f} GB free)")

    def check_keyring(self) -> Check:
        ok, name = self.credentials.available()
        if ok:
            return Check("keychain", OK, name)
        hint = "Windows uses Credential Manager automatically."
        if os.name != "nt":
            hint = (
                "Install a Secret Service provider (e.g. gnome-keyring) for cloud storage logins."
            )
        return Check("keychain", WARN, f"no system keychain ({name})", hint)


def _installed_ytdlp() -> str | None:
    try:
        from yt_dlp.version import __version__
    except ImportError:
        return None
    return str(__version__)
