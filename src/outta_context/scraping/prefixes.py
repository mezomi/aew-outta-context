"""Extra link prefixes per site, saved in prefixes.toml."""

from __future__ import annotations

import tomllib
from pathlib import Path

import tomli_w

from outta_context.core.errors import DuplicatePrefix, PrefixNotFound, SettingsError


class PrefixStore:
    def __init__(self, path: Path) -> None:
        self.path = path

    def _load(self) -> dict[str, list[str]]:
        if not self.path.exists():
            return {}
        try:
            data = tomllib.loads(self.path.read_text(encoding="utf-8"))
        except tomllib.TOMLDecodeError as exc:
            raise SettingsError(f"{self.path} is not valid TOML: {exc}") from exc
        sites = data.get("prefixes", {})
        return {str(site): [str(p) for p in items] for site, items in sites.items()}

    def _save(self, data: dict[str, list[str]]) -> None:
        self.path.parent.mkdir(parents=True, exist_ok=True)
        cleaned = {site: items for site, items in sorted(data.items()) if items}
        self.path.write_text(tomli_w.dumps({"prefixes": cleaned}), encoding="utf-8")

    def all(self, site: str | None = None) -> dict[str, list[str]]:
        data = self._load()
        if site is not None:
            return {site: data.get(site, [])}
        return data

    def for_site(self, site: str) -> list[str]:
        return self._load().get(site, [])

    def add(self, site: str, prefix: str) -> None:
        prefix = prefix.strip()
        if not prefix.lower().startswith(("http://", "https://", "//")):
            raise SettingsError(
                f'"{prefix}" is not a link prefix.',
                'Prefixes are the start of a link, e.g. "https://geo.dailymotion.com/player/".',
            )
        data = self._load()
        items = data.setdefault(site, [])
        if prefix in items:
            raise DuplicatePrefix(f'"{prefix}" is already registered for {site}.')
        items.append(prefix)
        self._save(data)

    def remove(self, site: str, prefix: str) -> None:
        data = self._load()
        items = data.get(site, [])
        if prefix.strip() not in items:
            raise PrefixNotFound(f'"{prefix}" is not registered for {site}.')
        items.remove(prefix.strip())
        self._save(data)
