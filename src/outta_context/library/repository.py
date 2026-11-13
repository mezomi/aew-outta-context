"""SQLite storage for clips, episodes, uploads and scraped links (library.db)."""

from __future__ import annotations

import sqlite3
from collections.abc import Iterator
from contextlib import contextmanager
from datetime import UTC, datetime
from pathlib import Path

from outta_context.core.errors import ClipNotFound, EpisodeExists, EpisodeNotFound
from outta_context.library.models import Clip, Episode, Upload

SCHEMA = """
CREATE TABLE IF NOT EXISTS episodes (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE COLLATE NOCASE,
    target_length REAL NOT NULL,
    state TEXT NOT NULL DEFAULT 'open',
    output_path TEXT,
    created_at TEXT NOT NULL,
    built_at TEXT
);
CREATE TABLE IF NOT EXISTS clips (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    title TEXT NOT NULL UNIQUE COLLATE NOCASE,
    source TEXT NOT NULL,
    source_url TEXT NOT NULL,
    start REAL NOT NULL,
    "end" REAL NOT NULL,
    duration REAL NOT NULL,
    file_path TEXT NOT NULL,
    page_url TEXT,
    created_at TEXT NOT NULL,
    episode_id INTEGER REFERENCES episodes(id),
    position INTEGER
);
CREATE TABLE IF NOT EXISTS uploads (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    kind TEXT NOT NULL,
    ref TEXT NOT NULL,
    local_path TEXT NOT NULL,
    storage TEXT NOT NULL,
    remote_path TEXT NOT NULL,
    size INTEGER NOT NULL,
    mtime REAL NOT NULL,
    uploaded_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS scraped_links (
    url TEXT PRIMARY KEY,
    page_url TEXT NOT NULL,
    kind TEXT NOT NULL,
    found_at TEXT NOT NULL
);
"""


def _now() -> str:
    return datetime.now(UTC).isoformat(timespec="seconds")


def _clip(row: sqlite3.Row) -> Clip:
    return Clip(
        id=row["id"],
        title=row["title"],
        source=row["source"],
        source_url=row["source_url"],
        start=row["start"],
        end=row["end"],
        duration=row["duration"],
        file_path=Path(row["file_path"]),
        page_url=row["page_url"],
        created_at=row["created_at"],
        episode_id=row["episode_id"],
        position=row["position"],
    )


def _episode(row: sqlite3.Row) -> Episode:
    return Episode(
        id=row["id"],
        name=row["name"],
        target_length=row["target_length"],
        state=row["state"],
        output_path=Path(row["output_path"]) if row["output_path"] else None,
        created_at=row["created_at"],
        built_at=row["built_at"],
    )


class LibraryRepository:
    def __init__(self, db_path: Path) -> None:
        self.db_path = db_path
        db_path.parent.mkdir(parents=True, exist_ok=True)
        with self._connect() as conn:
            conn.executescript(SCHEMA)

    @contextmanager
    def _connect(self) -> Iterator[sqlite3.Connection]:
        conn = sqlite3.connect(self.db_path)
        conn.row_factory = sqlite3.Row
        conn.execute("PRAGMA foreign_keys = ON")
        try:
            with conn:
                yield conn
        finally:
            conn.close()

    # --- clips -------------------------------------------------------------
    def add_clip(
        self,
        title: str,
        source: str,
        source_url: str,
        start: float,
        end: float,
        duration: float,
        file_path: Path,
        page_url: str | None = None,
    ) -> Clip:
        with self._connect() as conn:
            cur = conn.execute(
                'INSERT INTO clips (title, source, source_url, start, "end", duration, file_path,'
                " page_url, created_at) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
                (title, source, source_url, start, end, duration, str(file_path), page_url, _now()),
            )
            clip_id = int(cur.lastrowid or 0)
        return self.get_clip(clip_id)

    def get_clip(self, clip_id: int) -> Clip:
        with self._connect() as conn:
            row = conn.execute("SELECT * FROM clips WHERE id = ?", (clip_id,)).fetchone()
        if row is None:
            raise ClipNotFound(f"There is no clip with ID {clip_id}.")
        return _clip(row)

    def find_clip_by_title(self, title: str) -> Clip | None:
        with self._connect() as conn:
            row = conn.execute("SELECT * FROM clips WHERE title = ?", (title,)).fetchone()
        return _clip(row) if row else None

    def list_clips(self) -> list[Clip]:
        with self._connect() as conn:
            rows = conn.execute("SELECT * FROM clips ORDER BY id").fetchall()
        return [_clip(r) for r in rows]

    def episode_clips(self, episode_id: int) -> list[Clip]:
        with self._connect() as conn:
            rows = conn.execute(
                "SELECT * FROM clips WHERE episode_id = ? ORDER BY position, id", (episode_id,)
            ).fetchall()
        return [_clip(r) for r in rows]

    def attach_clip(self, clip_id: int, episode_id: int) -> None:
        with self._connect() as conn:
            (count,) = conn.execute(
                "SELECT COUNT(*) FROM clips WHERE episode_id = ?", (episode_id,)
            ).fetchone()
            conn.execute(
                "UPDATE clips SET episode_id = ?, position = ? WHERE id = ?",
                (episode_id, count + 1, clip_id),
            )

    def detach_clip(self, clip_id: int) -> None:
        clip = self.get_clip(clip_id)
        with self._connect() as conn:
            conn.execute(
                "UPDATE clips SET episode_id = NULL, position = NULL WHERE id = ?", (clip_id,)
            )
        if clip.episode_id is not None:
            self._renumber(clip.episode_id, [c.id for c in self.episode_clips(clip.episode_id)])

    def reorder(self, episode_id: int, clip_ids: list[int]) -> None:
        self._renumber(episode_id, clip_ids)

    def _renumber(self, episode_id: int, clip_ids: list[int]) -> None:
        with self._connect() as conn:
            for index, clip_id in enumerate(clip_ids, start=1):
                conn.execute(
                    "UPDATE clips SET position = ? WHERE id = ? AND episode_id = ?",
                    (index, clip_id, episode_id),
                )

    def update_clip_path(self, clip_id: int, file_path: Path) -> None:
        with self._connect() as conn:
            conn.execute("UPDATE clips SET file_path = ? WHERE id = ?", (str(file_path), clip_id))

    # --- episodes ----------------------------------------------------------
    def create_episode(self, name: str, target_length: float) -> Episode:
        if self.find_episode(name) is not None:
            raise EpisodeExists(f'An episode named "{name}" already exists.')
        with self._connect() as conn:
            cur = conn.execute(
                "INSERT INTO episodes (name, target_length, created_at) VALUES (?, ?, ?)",
                (name, target_length, _now()),
            )
            episode_id = int(cur.lastrowid or 0)
        return self.get_episode(episode_id)

    def get_episode(self, episode_id: int) -> Episode:
        with self._connect() as conn:
            row = conn.execute("SELECT * FROM episodes WHERE id = ?", (episode_id,)).fetchone()
        if row is None:
            raise EpisodeNotFound(f"There is no episode with ID {episode_id}.")
        return _episode(row)

    def find_episode(self, name: str) -> Episode | None:
        with self._connect() as conn:
            row = conn.execute("SELECT * FROM episodes WHERE name = ?", (name.strip(),)).fetchone()
        return _episode(row) if row else None

    def list_episodes(self) -> list[Episode]:
        with self._connect() as conn:
            rows = conn.execute("SELECT * FROM episodes ORDER BY id").fetchall()
        return [_episode(r) for r in rows]

    def latest_open_episode(self) -> Episode | None:
        with self._connect() as conn:
            row = conn.execute(
                "SELECT * FROM episodes WHERE state = 'open' ORDER BY id DESC LIMIT 1"
            ).fetchone()
        return _episode(row) if row else None

    def mark_built(self, episode_id: int, output_path: Path) -> Episode:
        with self._connect() as conn:
            conn.execute(
                "UPDATE episodes SET state = 'built', output_path = ?, built_at = ? WHERE id = ?",
                (str(output_path), _now(), episode_id),
            )
        return self.get_episode(episode_id)

    # --- uploads -----------------------------------------------------------
    def record_upload(
        self, kind: str, ref: str, local_path: Path, storage: str, remote_path: str
    ) -> None:
        stat = local_path.stat()
        with self._connect() as conn:
            conn.execute(
                "INSERT INTO uploads (kind, ref, local_path, storage, remote_path, size, mtime,"
                " uploaded_at) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                (
                    kind,
                    ref,
                    str(local_path.resolve()),
                    storage,
                    remote_path,
                    stat.st_size,
                    stat.st_mtime,
                    _now(),
                ),
            )

    def find_upload(self, local_path: Path, storage: str, remote_path: str) -> Upload | None:
        stat = local_path.stat()
        with self._connect() as conn:
            row = conn.execute(
                "SELECT * FROM uploads WHERE local_path = ? AND storage = ? AND remote_path = ?"
                " AND size = ? AND abs(mtime - ?) < 0.001 ORDER BY id DESC LIMIT 1",
                (str(local_path.resolve()), storage, remote_path, stat.st_size, stat.st_mtime),
            ).fetchone()
        if row is None:
            return None
        return Upload(
            id=row["id"],
            kind=row["kind"],
            ref=row["ref"],
            local_path=Path(row["local_path"]),
            storage=row["storage"],
            remote_path=row["remote_path"],
            size=row["size"],
            uploaded_at=row["uploaded_at"],
        )

    def uploads_for(self, kind: str, ref: str) -> list[str]:
        with self._connect() as conn:
            rows = conn.execute(
                "SELECT DISTINCT storage FROM uploads WHERE kind = ? AND ref = ?", (kind, ref)
            ).fetchall()
        return [r["storage"] for r in rows]

    # --- scraped links -----------------------------------------------------
    def remember_links(self, links: list[tuple[str, str, str]]) -> None:
        """Save (video_url, page_url, kind) so `outta clip` can send the page as referer."""
        with self._connect() as conn:
            conn.executemany(
                "INSERT OR REPLACE INTO scraped_links (url, page_url, kind, found_at)"
                " VALUES (?, ?, ?, ?)",
                [(url, page, kind, _now()) for url, page, kind in links],
            )

    def referer_for(self, video_url: str) -> str | None:
        with self._connect() as conn:
            row = conn.execute(
                "SELECT page_url FROM scraped_links WHERE url = ?", (video_url,)
            ).fetchone()
        return str(row["page_url"]) if row else None
