"""EpisodeService: the current episode, adding clips, status, reorder and build bookkeeping."""

from __future__ import annotations

import re
from pathlib import Path

from outta_context.core.errors import (
    ClipNotFound,
    EpisodeBuilt,
    EpisodeExists,
    EpisodeNotFound,
    InvalidPosition,
)
from outta_context.library.models import Clip, Episode, EpisodeStatus
from outta_context.library.repository import LibraryRepository

_NUMBERED = re.compile(r"^Episode (\d+)$", re.I)


class EpisodeService:
    def __init__(self, repo: LibraryRepository, target_length: float) -> None:
        self.repo = repo
        self.target_length = target_length

    def next_name(self) -> str:
        numbers = [
            int(m.group(1)) for e in self.repo.list_episodes() if (m := _NUMBERED.match(e.name))
        ]
        return f"Episode {max(numbers, default=0) + 1}"

    def current(self) -> Episode:
        """The open episode new clips go into; created as "Episode 1" on first use."""
        episode = self.repo.latest_open_episode()
        if episode is None:
            episode = self.repo.create_episode(self.next_name(), self.target_length)
        return episode

    def get(self, name: str | None = None) -> Episode:
        if name is None:
            return self.current()
        episode = self.repo.find_episode(name)
        if episode is None:
            raise EpisodeNotFound(f'There is no episode named "{name}".')
        return episode

    def new(self, name: str) -> Episode:
        name = " ".join(name.split())
        if not name:
            raise EpisodeExists("The episode name is empty.", 'Give it a name, e.g. "Episode 4".')
        return self.repo.create_episode(name, self.target_length)

    def status(self, name: str | None = None) -> EpisodeStatus:
        episode = self.get(name)
        return EpisodeStatus(episode, self.repo.episode_clips(episode.id))

    def all_statuses(self) -> list[EpisodeStatus]:
        self.current()
        return [EpisodeStatus(e, self.repo.episode_clips(e.id)) for e in self.repo.list_episodes()]

    def add_clip(self, clip: Clip, name: str | None = None) -> EpisodeStatus:
        episode = self.get(name)
        if episode.is_built:
            raise EpisodeBuilt(f'"{episode.name}" is already built.')
        self.repo.attach_clip(clip.id, episode.id)
        return self.status(episode.name)

    def _episode_of(self, clip_id: int) -> tuple[Clip, Episode]:
        clip = self.repo.get_clip(clip_id)
        if clip.episode_id is None:
            raise ClipNotFound(f'Clip {clip_id} ("{clip.title}") is not in an episode.')
        return clip, self.repo.get_episode(clip.episode_id)

    def move(self, clip_id: int, position: int) -> EpisodeStatus:
        clip, episode = self._episode_of(clip_id)
        ids = [c.id for c in self.repo.episode_clips(episode.id)]
        if not 1 <= position <= len(ids):
            raise InvalidPosition(
                f'Position {position} is outside 1-{len(ids)} for "{episode.name}".'
            )
        ids.remove(clip.id)
        ids.insert(position - 1, clip.id)
        self.repo.reorder(episode.id, ids)
        return self.status(episode.name)

    def remove(self, clip_id: int) -> tuple[Clip, EpisodeStatus]:
        clip, episode = self._episode_of(clip_id)
        self.repo.detach_clip(clip.id)
        return clip, self.status(episode.name)

    def finish(self, episode: Episode, output_path: Path) -> tuple[Episode, Episode | None]:
        """Mark built; open the next numbered episode if no other episode is open."""
        built = self.repo.mark_built(episode.id, output_path)
        opened = None
        if self.repo.latest_open_episode() is None:
            opened = self.repo.create_episode(self.next_name(), self.target_length)
        return built, opened
