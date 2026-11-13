"""Load and save settings.toml. Unknown keys are kept out; bad values give clear errors."""

from __future__ import annotations

import tomllib
from pathlib import Path
from typing import Any

import tomli_w

from outta_context.core.errors import SettingsError
from outta_context.core.timerange import format_length, format_time, parse_time
from outta_context.settings.models import Settings

# CLI key -> (toml key, description)
KEYS: dict[str, str] = {
    "destination": "Folder where clips are saved locally",
    "episode-length": "Target episode length, e.g. 10:00",
    "max-clip-length": "Longest clip `outta clip` accepts (catches typos); `off` for no cap",
    "max-quality": "Highest video height to download: 360, 480, 720, 1080, 1440 or 2160",
    "default-storage": "Storage name used by --upload and `outta upload`",
}
QUALITIES = (360, 480, 720, 1080, 1440, 2160)


def parse_value(settings: Settings, key: str, raw: str) -> None:
    """Apply `outta config set KEY VALUE` to a Settings object."""
    key = key.strip().lower().replace("_", "-")
    if key in ("destination", "clip-folder"):
        settings.clip_folder = Path(raw).expanduser()
    elif key == "episode-length":
        try:
            seconds = parse_time(raw)
        except Exception as exc:
            raise SettingsError(
                f'"{raw}" is not a length.',
                "Use M:SS, e.g. `outta config set episode-length 10:00`.",
            ) from exc
        if seconds < 10:
            raise SettingsError("Episode length must be at least 10 seconds.", "e.g. 10:00")
        settings.episode_length = seconds
    elif key in ("max-clip-length", "clip-cap"):
        if raw.strip().lower() in ("off", "none", "0", ""):
            settings.max_clip_length = None
            return
        try:
            seconds = parse_time(raw)
        except Exception as exc:
            raise SettingsError(
                f'"{raw}" is not a length.',
                "Use seconds or M:SS, e.g. `outta config set max-clip-length 45` (or `off`).",
            ) from exc
        settings.max_clip_length = seconds
    elif key == "max-quality":
        value = raw.lower().rstrip("p")
        if not value.isdigit() or int(value) not in QUALITIES:
            raise SettingsError(
                f'"{raw}" is not a supported quality.',
                "Use one of: " + ", ".join(str(q) for q in QUALITIES),
            )
        settings.max_quality = int(value)
    elif key == "default-storage":
        settings.default_storage = raw or None
    else:
        raise SettingsError(
            f'Unknown setting "{key}".', "Settings you can change: " + ", ".join(KEYS)
        )


def as_display(settings: Settings) -> dict[str, str]:
    return {
        "destination": str(settings.clip_folder),
        "episode-length": format_time(settings.episode_length),
        "max-clip-length": format_length(settings.max_clip_length)
        if settings.max_clip_length
        else "off",
        "max-quality": f"{settings.max_quality}p",
        "default-storage": settings.default_storage or "(none)",
    }


class SettingsStore:
    def __init__(self, path: Path) -> None:
        self.path = path

    def load(self) -> Settings:
        settings = Settings()
        if not self.path.exists():
            return settings
        try:
            data: dict[str, Any] = tomllib.loads(self.path.read_text(encoding="utf-8"))
        except (tomllib.TOMLDecodeError, UnicodeDecodeError) as exc:
            raise SettingsError(f"Settings file {self.path} is not valid TOML: {exc}") from exc
        try:
            if "clip_folder" in data:
                settings.clip_folder = Path(str(data["clip_folder"])).expanduser()
            if "episode_length" in data:
                settings.episode_length = float(data["episode_length"])
            if "max_quality" in data:
                settings.max_quality = int(data["max_quality"])
            if "max_clip_length" in data:
                cap = float(data["max_clip_length"])
                settings.max_clip_length = cap if cap > 0 else None
            if data.get("default_storage"):
                settings.default_storage = str(data["default_storage"])
        except (TypeError, ValueError) as exc:
            raise SettingsError(f"Settings file {self.path} has a bad value: {exc}") from exc
        return settings

    def save(self, settings: Settings) -> None:
        data: dict[str, Any] = {
            "clip_folder": str(settings.clip_folder),
            "episode_length": settings.episode_length,
            "max_quality": settings.max_quality,
            "max_clip_length": settings.max_clip_length or 0,
        }
        if settings.default_storage:
            data["default_storage"] = settings.default_storage
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.path.write_text(tomli_w.dumps(data), encoding="utf-8")

    def set_value(self, key: str, raw: str) -> Settings:
        settings = self.load()
        parse_value(settings, key, raw)
        self.save(settings)
        return settings
