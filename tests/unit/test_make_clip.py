from pathlib import Path

import pytest

from outta_context.core.errors import (
    ClipTooLong,
    DownloadFailed,
    DuplicateTitle,
    EpisodeBuilt,
    InvalidTimeRange,
    InvalidTitle,
    RangeOutsideVideo,
    UnsupportedUrl,
)
from outta_context.core.reporter import RecordingReporter
from outta_context.library.episodes import EpisodeService
from outta_context.library.repository import LibraryRepository
from outta_context.settings.models import Settings
from outta_context.sources.registry import SourceRegistry
from outta_context.workflows.make_clip import ClipRequest, MakeClipWorkflow
from tests.conftest import FakeDownloader, FakeFfmpeg

YT = "https://youtu.be/dQw4w9WgXcQ"


class FakeUploader:
    def __init__(self) -> None:
        self.clips: list[tuple[int, str | None]] = []

    def upload_clip(self, clip: object, storage: str | None = None) -> str:
        self.clips.append((clip.id, storage))  # type: ignore[attr-defined]
        return "remote"


def make(
    repo: LibraryRepository,
    episodes: EpisodeService,
    settings: Settings,
    reporter: RecordingReporter,
    downloader: FakeDownloader | None = None,
    ffmpeg: FakeFfmpeg | None = None,
    uploader: FakeUploader | None = None,
) -> MakeClipWorkflow:
    return MakeClipWorkflow(
        SourceRegistry(),
        downloader or FakeDownloader(),
        ffmpeg or FakeFfmpeg(),  # type: ignore[arg-type]
        repo,
        episodes,
        settings,
        reporter,
        uploader,
    )  # type: ignore[arg-type]


def test_happy_path(repo, episodes, settings, reporter) -> None:  # type: ignore[no-untyped-def]
    dl = FakeDownloader()
    result = make(repo, episodes, settings, reporter, dl).run(
        ClipRequest(YT, "0:45-1:05", "Table spot")
    )
    clip = result.clip
    assert clip.title == "Table spot"
    assert clip.file_path == settings.clip_folder / "Table spot.mp4"
    assert clip.file_path.exists()
    assert clip.source == "youtube"
    assert clip.source_url == "https://www.youtube.com/watch?v=dQw4w9WgXcQ"
    assert (clip.start, clip.end, clip.duration) == (45, 65, 20)
    assert dl.calls[0]["url"] == "https://www.youtube.com/watch?v=dQw4w9WgXcQ"
    assert dl.calls[1]["quality"] == 1080
    steps = reporter.texts("step")
    assert steps[0] == "Detected source: YouTube"
    assert steps[1] == "Reading video info..."
    assert steps[2] == 'Video: "AEW Dynamite highlights" (10:00)'
    assert steps[3].startswith("Downloading 0:45-1:05 (20s, best up to 1080p")
    assert reporter.texts("success")[1] == 'Added to "Episode 1": 0:20 of 10:00 (9:40 left)'
    assert result.status is not None
    assert result.status.total == 20


def test_title_is_required_and_cleaned(repo, episodes, settings, reporter) -> None:  # type: ignore[no-untyped-def]
    wf = make(repo, episodes, settings, reporter)
    with pytest.raises(InvalidTitle):
        wf.run(ClipRequest(YT, "0-5", "   "))
    result = wf.run(ClipRequest(YT, "0-5", '  What?  "Really"  '))
    assert result.clip.title == 'What? "Really"'
    assert result.clip.file_path.name == "What_ _Really_.mp4"


def test_duplicate_title_refused_before_download(repo, episodes, settings, reporter) -> None:  # type: ignore[no-untyped-def]
    dl = FakeDownloader()
    wf = make(repo, episodes, settings, reporter, dl)
    wf.run(ClipRequest(YT, "0-5", "Same"))
    n = len(dl.calls)
    with pytest.raises(DuplicateTitle, match="already exists"):
        wf.run(ClipRequest(YT, "5-10", "same"))
    assert len(dl.calls) == n
    (settings.clip_folder / "On disk.mp4").write_bytes(b"x")
    with pytest.raises(DuplicateTitle, match="already exists"):
        wf.run(ClipRequest(YT, "5-10", "On disk"))


def test_clip_cap_blocks_mistyped_ranges_before_download(
    repo, episodes, settings, reporter
) -> None:  # type: ignore[no-untyped-def]
    dl = FakeDownloader()
    wf = make(repo, episodes, settings, reporter, dl)
    with pytest.raises(ClipTooLong, match="over the 45s clip cap") as exc:
        wf.run(ClipRequest(YT, "1:30-11:52", "Typo"))  # meant 1:30-1:52
    assert "max-clip-length" in exc.value.hint
    assert dl.calls == []
    wf.run(ClipRequest(YT, "0:00-0:45", "Exactly cap"))
    settings.max_clip_length = 120
    wf.run(ClipRequest(YT, "1:00-2:30", "Longer ok"))
    settings.max_clip_length = None
    wf.run(ClipRequest(YT, "0:00-5:00", "No cap"))


def test_range_checked_against_video_length(repo, episodes, settings, reporter) -> None:  # type: ignore[no-untyped-def]
    dl = FakeDownloader(duration=30)
    wf = make(repo, episodes, settings, reporter, dl)
    with pytest.raises(RangeOutsideVideo, match="past the end"):
        wf.run(ClipRequest(YT, "0:20-0:40", "Too far"))
    assert [c["op"] for c in dl.calls] == ["info"]  # nothing downloaded
    with pytest.raises(RangeOutsideVideo):
        wf.run(ClipRequest(YT, "0:31-0:35", "Start past end"))
    assert repo.list_clips() == []


def test_unknown_duration_warns(repo, episodes, settings, reporter) -> None:  # type: ignore[no-untyped-def]
    make(repo, episodes, settings, reporter, FakeDownloader(duration=None)).run(
        ClipRequest(YT, "0-5", "Live")
    )
    assert any("did not report" in w for w in reporter.texts("warn"))


def test_bad_input_raises_before_network(repo, episodes, settings, reporter) -> None:  # type: ignore[no-untyped-def]
    dl = FakeDownloader()
    wf = make(repo, episodes, settings, reporter, dl)
    with pytest.raises(InvalidTimeRange):
        wf.run(ClipRequest(YT, "1:52-1:30", "x"))
    with pytest.raises(UnsupportedUrl):
        wf.run(ClipRequest("https://vimeo.com/1", "0-5", "x"))
    assert dl.calls == []


def test_failed_download_adds_nothing(repo, episodes, settings, reporter) -> None:  # type: ignore[no-untyped-def]
    dl = FakeDownloader(fail_with=DownloadFailed("nope"))
    with pytest.raises(DownloadFailed):
        make(repo, episodes, settings, reporter, dl).run(ClipRequest(YT, "0-5", "x"))
    assert repo.list_clips() == []
    assert episodes.status().clips == []


def test_episode_options(repo, episodes, settings, reporter) -> None:  # type: ignore[no-untyped-def]
    wf = make(repo, episodes, settings, reporter)
    episodes.current()
    episodes.new("Special")
    r = wf.run(ClipRequest(YT, "0-5", "a", episode="Episode 1"))
    assert r.status is not None
    assert r.status.episode.name == "Episode 1"
    r = wf.run(ClipRequest(YT, "0-5", "b", no_episode=True))
    assert r.status is None
    assert r.clip.episode_id is None
    episodes.finish(episodes.get("Episode 1"), Path("/x.mp4"))
    with pytest.raises(EpisodeBuilt):
        wf.run(ClipRequest(YT, "0-5", "c", episode="Episode 1"))


def test_ready_to_build_warning_and_measured_length(repo, episodes, settings, reporter) -> None:  # type: ignore[no-untyped-def]
    settings.max_clip_length = None
    episodes.target_length = 30
    ff = FakeFfmpeg(durations={"a.mp4": 19.9, "b.mp4": 15.0})
    wf = make(repo, episodes, settings, reporter, ffmpeg=ff)
    wf.run(ClipRequest(YT, "0-20", "a"))
    r = wf.run(ClipRequest(YT, "0-15", "b"))
    assert r.clip.duration == 15.0
    assert r.status is not None
    assert r.status.ready
    assert "ready to build" in reporter.texts("success")[-1]
    assert any("outta episode build" in w for w in reporter.texts("warn"))


def test_without_ffprobe_uses_requested_length(repo, episodes, settings, reporter) -> None:  # type: ignore[no-untyped-def]
    r = make(repo, episodes, settings, reporter, ffmpeg=FakeFfmpeg(is_available=False)).run(
        ClipRequest(YT, "0-12", "a")
    )
    assert r.clip.duration == 12


def test_referer_from_scrape_history_and_upload(repo, episodes, settings, reporter) -> None:  # type: ignore[no-untyped-def]
    repo.remember_links(
        [("https://www.dailymotion.com/video/x8abc12", "https://news/page", "embed")]
    )
    dl = FakeDownloader()
    up = FakeUploader()
    r = make(repo, episodes, settings, reporter, dl, uploader=up).run(
        ClipRequest("https://dai.ly/x8abc12", "0-5", "dm", upload=True, storage="drive")
    )
    assert dl.calls[0]["referer"] == "https://news/page"
    assert dl.calls[1]["referer"] == "https://news/page"
    assert r.clip.page_url == "https://news/page"
    assert up.clips == [(r.clip.id, "drive")]
    dl2 = FakeDownloader()
    make(repo, episodes, settings, reporter, dl2).run(
        ClipRequest("https://dai.ly/x8abc12", "5-10", "dm2", referer="https://explicit")
    )
    assert dl2.calls[0]["referer"] == "https://explicit"
