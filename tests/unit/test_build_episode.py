from pathlib import Path

import pytest

from outta_context.core.errors import ClipFileMissing, EpisodeEmpty, FfmpegMissing, OuttaError
from outta_context.core.reporter import RecordingReporter
from outta_context.library.episodes import EpisodeService
from outta_context.library.repository import LibraryRepository
from outta_context.settings.models import Settings
from outta_context.workflows.build_episode import BuildEpisodeWorkflow
from tests.conftest import FakeFfmpeg


def add_clip(
    repo: LibraryRepository, episodes: EpisodeService, settings: Settings, title: str
) -> Path:
    path = settings.clip_folder / f"{title}.mp4"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(title.encode())
    clip = repo.add_clip(title, "youtube", "u", 0, 20, 20, path)
    episodes.add_clip(clip)
    return path


class FakeUploader:
    def __init__(self) -> None:
        self.calls: list[tuple[str, int, str | None]] = []

    def upload_episode(self, episode, clips, storage=None):  # type: ignore[no-untyped-def]
        self.calls.append((episode.name, len(clips), storage))
        return []


def make(episodes, settings, tmp_path, ff, reporter, uploader=None):  # type: ignore[no-untyped-def]
    return BuildEpisodeWorkflow(episodes, ff, settings, tmp_path / "cache", reporter, uploader)


def test_build(repo, episodes, settings, tmp_path, reporter: RecordingReporter) -> None:  # type: ignore[no-untyped-def]
    for t in ("a", "b", "c"):
        add_clip(repo, episodes, settings, t)
    ff = FakeFfmpeg()
    result = make(episodes, settings, tmp_path, ff, reporter).run()
    assert [src.name for src, _ in ff.normalized] == ["a.mp4", "b.mp4", "c.mp4"]
    assert result.output == settings.clip_folder / "Episodes" / "Episode 1.mp4"
    assert result.output.exists()
    assert result.duration == 60
    assert result.episode.is_built
    assert result.next_episode is not None
    assert result.next_episode.name == "Episode 2"
    assert episodes.current().name == "Episode 2"
    assert reporter.texts("step")[0] == 'Building "Episode 1": 3 clips, 1:00'
    assert reporter.texts("success")[-1] == 'Opened "Episode 2" for new clips'


def test_rebuild_after_reorder_uses_cache(repo, episodes, settings, tmp_path, reporter) -> None:  # type: ignore[no-untyped-def]
    for t in ("a", "b"):
        add_clip(repo, episodes, settings, t)
    ff = FakeFfmpeg()
    wf = make(episodes, settings, tmp_path, ff, reporter)
    wf.run()
    episodes.move(2, 1)
    wf.run("Episode 1")
    assert len(ff.normalized) == 2  # nothing converted twice
    assert [p.name.split("-")[0] for p in ff.concats[-1][0]] == ["clip2", "clip1"]
    assert any("using converted copy" in s for s in reporter.texts("step"))
    # touching a clip file invalidates its cached copy
    (settings.clip_folder / "a.mp4").write_bytes(b"changed clip")
    wf.run("Episode 1")
    assert len(ff.normalized) == 3


def test_errors(repo, episodes, settings, tmp_path, reporter) -> None:  # type: ignore[no-untyped-def]
    with pytest.raises(EpisodeEmpty):
        make(episodes, settings, tmp_path, FakeFfmpeg(), reporter).run()
    path = add_clip(repo, episodes, settings, "a")
    with pytest.raises(FfmpegMissing):
        make(episodes, settings, tmp_path, FakeFfmpeg(is_available=False), reporter).run()
    with pytest.raises(OuttaError, match="--clean-local only works"):
        make(episodes, settings, tmp_path, FakeFfmpeg(), reporter).run(clean_local=True)
    path.unlink()
    with pytest.raises(ClipFileMissing, match="missing its file"):
        make(episodes, settings, tmp_path, FakeFfmpeg(), reporter).run()


def test_upload_and_clean_local(repo, episodes, settings, tmp_path, reporter) -> None:  # type: ignore[no-untyped-def]
    a = add_clip(repo, episodes, settings, "a")
    up = FakeUploader()
    result = make(episodes, settings, tmp_path, FakeFfmpeg(), reporter, up).run(
        upload=True, storage="drive", clean_local=True
    )
    assert up.calls == [("Episode 1", 1, "drive")]
    assert not a.exists()
    assert result.output.exists()  # the episode itself is kept
    assert "Deleted 1 local clip files" in reporter.texts("success")[-1]
