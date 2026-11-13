from pathlib import Path

import pytest

from outta_context.core.errors import (
    ClipNotFound,
    EpisodeBuilt,
    EpisodeExists,
    EpisodeNotFound,
    InvalidPosition,
)
from outta_context.library.episodes import EpisodeService
from outta_context.library.models import Clip
from outta_context.library.repository import LibraryRepository


def add(repo: LibraryRepository, title: str, duration: float = 60.0) -> Clip:
    return repo.add_clip(
        title, "youtube", "https://y/" + title, 0, duration, duration, Path(f"/c/{title}.mp4")
    )


def test_first_episode_is_created(episodes: EpisodeService) -> None:
    ep = episodes.current()
    assert ep.name == "Episode 1"
    assert ep.state == "open"
    assert ep.target_length == 600
    assert episodes.current().id == ep.id


def test_add_clips_totals_and_order(repo: LibraryRepository, episodes: EpisodeService) -> None:
    for t, d in [("a", 100), ("b", 200), ("c", 150)]:
        status = episodes.add_clip(add(repo, t, d))
    assert [c.title for c in status.clips] == ["a", "b", "c"]
    assert [c.position for c in status.clips] == [1, 2, 3]
    assert status.total == 450
    assert status.remaining == 150
    assert not status.ready
    status = episodes.add_clip(add(repo, "d", 200))
    assert status.total == 650
    assert status.remaining == 0
    assert status.ready


def test_titles_unique_case_insensitive(repo: LibraryRepository) -> None:
    add(repo, "Table spot")
    assert repo.find_clip_by_title("table SPOT") is not None
    assert repo.find_clip_by_title("other") is None
    with pytest.raises(Exception, match="UNIQUE"):
        add(repo, "TABLE SPOT")


def test_move_and_remove(repo: LibraryRepository, episodes: EpisodeService) -> None:
    clips = [add(repo, t) for t in "abcd"]
    for c in clips:
        episodes.add_clip(c)
    status = episodes.move(clips[3].id, 1)
    assert [c.title for c in status.clips] == ["d", "a", "b", "c"]
    status = episodes.move(clips[0].id, 4)
    assert [c.title for c in status.clips] == ["d", "b", "c", "a"]
    with pytest.raises(InvalidPosition):
        episodes.move(clips[0].id, 5)
    with pytest.raises(InvalidPosition):
        episodes.move(clips[0].id, 0)
    removed, status = episodes.remove(clips[1].id)
    assert removed.title == "b"
    assert [(c.title, c.position) for c in status.clips] == [("d", 1), ("c", 2), ("a", 3)]
    assert repo.get_clip(clips[1].id).episode_id is None  # clip record (and file) kept
    with pytest.raises(ClipNotFound, match="not in an episode"):
        episodes.remove(clips[1].id)
    with pytest.raises(ClipNotFound):
        episodes.move(999, 1)


def test_named_episodes(repo: LibraryRepository, episodes: EpisodeService) -> None:
    episodes.current()
    special = episodes.new("  All Out   special ")
    assert special.name == "All Out special"
    assert episodes.current().id == special.id  # newest open episode is current
    with pytest.raises(EpisodeExists):
        episodes.new("all out special")
    with pytest.raises(EpisodeExists):
        episodes.new("   ")
    episodes.add_clip(add(repo, "x"), "Episode 1")
    assert len(episodes.status("Episode 1").clips) == 1
    with pytest.raises(EpisodeNotFound):
        episodes.status("Nope")
    assert episodes.next_name() == "Episode 2"


def test_finish_marks_built_and_opens_next(
    repo: LibraryRepository, episodes: EpisodeService
) -> None:
    ep = episodes.current()
    episodes.add_clip(add(repo, "a"))
    built, opened = episodes.finish(ep, Path("/out/Episode 1.mp4"))
    assert built.is_built
    assert built.output_path == Path("/out/Episode 1.mp4")
    assert built.built_at
    assert opened is not None
    assert opened.name == "Episode 2"
    assert episodes.current().id == opened.id
    with pytest.raises(EpisodeBuilt):
        episodes.add_clip(add(repo, "b"), "Episode 1")
    # Rebuilding a built episode while another is open opens nothing new
    _, again = episodes.finish(built, Path("/out/Episode 1.mp4"))
    assert again is None
    assert [s.episode.name for s in episodes.all_statuses()] == ["Episode 1", "Episode 2"]


def test_uploads_and_referers(repo: LibraryRepository, tmp_path: Path) -> None:
    f = tmp_path / "a.mp4"
    f.write_bytes(b"123")
    assert repo.find_upload(f, "drive", "a.mp4") is None
    repo.record_upload("clip", "1", f, "drive", "a.mp4")
    up = repo.find_upload(f, "drive", "a.mp4")
    assert up is not None
    assert up.size == 3
    assert repo.find_upload(f, "other", "a.mp4") is None
    assert repo.uploads_for("clip", "1") == ["drive"]
    f.write_bytes(b"changed!")
    assert repo.find_upload(f, "drive", "a.mp4") is None
    repo.remember_links([("https://v/1", "https://page", "embed")])
    assert repo.referer_for("https://v/1") == "https://page"
    assert repo.referer_for("https://v/2") is None


def test_repo_misc(repo: LibraryRepository) -> None:
    c = add(repo, "a")
    repo.update_clip_path(c.id, Path("/new.mp4"))
    assert repo.get_clip(c.id).file_path == Path("/new.mp4")
    assert [x.title for x in repo.list_clips()] == ["a"]
    with pytest.raises(EpisodeNotFound):
        repo.get_episode(42)
    with pytest.raises(ClipNotFound):
        repo.get_clip(42)
