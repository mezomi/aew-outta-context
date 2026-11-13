"""Configured storages (storage.toml) and the backend class for each provider."""

from __future__ import annotations

import re
import tomllib
from collections.abc import Callable
from pathlib import Path
from typing import Any

import tomli_w

from outta_context.core.errors import SettingsError, StorageExists, StorageNotConfigured
from outta_context.storage.base import StorageBackend, StorageConfig
from outta_context.storage.local import LocalBackend
from outta_context.storage.rclone import RCLONE_TYPES, Rclone, RcloneBackend

PROVIDERS = ("gdrive", "onedrive", "dropbox", "b2", "local")
PROVIDER_LABELS = {
    "gdrive": "Google Drive",
    "onedrive": "OneDrive",
    "dropbox": "Dropbox",
    "b2": "Backblaze B2",
    "local": "Local folder",
}


class StorageRegistry:
    def __init__(self, path: Path, rclone_factory: Callable[[], Rclone]) -> None:
        self.path = path
        self._rclone_factory = rclone_factory
        self._rclone: Rclone | None = None

    @property
    def rclone(self) -> Rclone:
        if self._rclone is None:
            self._rclone = self._rclone_factory()
        return self._rclone

    def _load(self) -> dict[str, StorageConfig]:
        if not self.path.exists():
            return {}
        try:
            data: dict[str, Any] = tomllib.loads(self.path.read_text(encoding="utf-8"))
        except tomllib.TOMLDecodeError as exc:
            raise SettingsError(f"{self.path} is not valid TOML: {exc}") from exc
        result = {}
        for name, item in data.get("storage", {}).items():
            result[name] = StorageConfig(
                name=name,
                provider=str(item.get("provider", "")),
                folder=str(item.get("folder", "")),
                options={str(k): str(v) for k, v in item.get("options", {}).items()},
            )
        return result

    def _save(self, configs: dict[str, StorageConfig]) -> None:
        self.path.parent.mkdir(parents=True, exist_ok=True)
        data = {"storage": {name: c.as_dict() for name, c in sorted(configs.items())}}
        self.path.write_text(tomli_w.dumps(data), encoding="utf-8")

    def list(self) -> list[StorageConfig]:
        return list(self._load().values())

    def get(self, name: str) -> StorageConfig:
        configs = self._load()
        if name not in configs:
            raise StorageNotConfigured(
                f'No storage named "{name}".', "See storages with `outta storage list`."
            )
        return configs[name]

    @staticmethod
    def validate_name(name: str) -> str:
        if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_-]{0,31}", name):
            raise SettingsError(
                f'"{name}" is not a valid storage name.',
                "Use letters, numbers, - and _ (up to 32), e.g. drive.",
            )
        return name

    def add(self, config: StorageConfig) -> None:
        configs = self._load()
        if config.name in configs:
            raise StorageExists(f'A storage named "{config.name}" already exists.')
        configs[config.name] = config
        self._save(configs)

    def remove(self, name: str) -> StorageConfig:
        configs = self._load()
        config = self.get(name)
        del configs[name]
        self._save(configs)
        return config

    def backend(self, config: StorageConfig) -> StorageBackend:
        if config.provider == "local":
            return LocalBackend(config)
        if config.provider in RCLONE_TYPES:
            return RcloneBackend(config, self.rclone)
        raise SettingsError(
            f'Storage "{config.name}" has unknown provider "{config.provider}".',
            "Providers: " + ", ".join(PROVIDERS),
        )
