"""Where the tool keeps its data. Set OUTTA_HOME to override (tests and portable use)."""

from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path

from platformdirs import user_data_dir

APP_NAME = "outta-context"


@dataclass(frozen=True)
class AppPaths:
    data_dir: Path

    @classmethod
    def default(cls) -> AppPaths:
        override = os.environ.get("OUTTA_HOME")
        if override:
            return cls(Path(override).expanduser())
        return cls(Path(user_data_dir(APP_NAME, appauthor=False, roaming=False)))

    @property
    def settings_file(self) -> Path:
        return self.data_dir / "settings.toml"

    @property
    def prefixes_file(self) -> Path:
        return self.data_dir / "prefixes.toml"

    @property
    def library_db(self) -> Path:
        return self.data_dir / "library.db"

    @property
    def storage_file(self) -> Path:
        return self.data_dir / "storage.toml"

    @property
    def rclone_config(self) -> Path:
        return self.data_dir / "rclone.conf"

    @property
    def cache_dir(self) -> Path:
        return self.data_dir / "cache"

    def ensure(self) -> None:
        self.data_dir.mkdir(parents=True, exist_ok=True)
