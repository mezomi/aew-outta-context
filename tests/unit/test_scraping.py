from pathlib import Path

import httpx
import pytest

from outta_context.core.errors import (
    DuplicatePrefix,
    FetchFailed,
    NoVideosFound,
    PrefixNotFound,
    SettingsError,
    UnknownSite,
)
from outta_context.core.reporter import RecordingReporter
from outta_context.library.repository import LibraryRepository
from outta_context.scraping.base import id_from_link
from outta_context.scraping.fetch import HttpxFetcher
from outta_context.scraping.prefixes import PrefixStore
from outta_context.scraping.registry import ScraperRegistry
from outta_context.workflows.scrape import ScrapeWorkflow
from tests.conftest import FakePageFetcher, fixture_page

DM = "https://www.dailymotion.com/video/"


def test_dailymotion_fixture_finds_expected_ids_in_order() -> None:
    found = (
        ScraperRegistry()
        .get("dailymotion")
        .extract(fixture_page("dailymotion_article.html"), "https://p")
    )
    assert [f.video_id for f in found] == [
        "k1ojSwOaH7psOtMbpGS",
        "x8abc12",
        "x7short",
        "x6script",
        "x5json",
    ]
    assert found[0].kind == "geo player embed"
    assert found[1].kind == "embed iframe"
    assert found[0].url == DM + "k1ojSwOaH7psOtMbpGS"
    assert all(f.page_url == "https://p" for f in found)


def test_youtube_fixture_finds_expected_ids_without_duplicates() -> None:
    found = (
        ScraperRegistry().get("youtube").extract(fixture_page("youtube_article.html"), "https://p")
    )
    assert [f.video_id for f in found] == [
        "dQw4w9WgXcQ",
        "9bZkp7q19f0",
        "kJQP7kiw5Fk",
        "aqz-KE-bpKQ",
    ]
    assert found[0].url == "https://www.youtube.com/watch?v=dQw4w9WgXcQ"


def test_data_video_needs_dailymotion_on_page() -> None:
    scraper = ScraperRegistry().get("dailymotion")
    assert scraper.extract('<div data-video="x6script"></div>', "p") == []


def test_registered_prefix_matches() -> None:
    scraper = ScraperRegistry().get("dailymotion")
    page = fixture_page("custom_embed.html")
    assert scraper.extract(page, "p") == []
    found = scraper.extract(page, "p", ["https://players.example-cdn.com/"])
    assert [f.video_id for f in found] == ["x4custom1", "x4custom2"]
    assert {f.kind for f in found} == {"registered prefix"}
    # scheme-insensitive
    assert len(scraper.extract(page, "p", ["http://players.example-cdn.com/v/"])) == 1


@pytest.mark.parametrize(
    ("link", "vid"),
    [
        ("https://x.com/player.html?video=abc12", "abc12"),
        ("https://x.com/w?v=dQw4w9WgXcQ", "dQw4w9WgXcQ"),
        ("https://x.com/v/abc123.html", "abc123"),
        ("//x.com/e/abc123/", "abc123"),
        ("https://x.com/", None),
        ("https://x.com/?video=a b", None),
        ("https://x.com/ab", None),
    ],
)
def test_id_from_link(link: str, vid: str | None) -> None:
    assert id_from_link(link) == vid


def test_registry() -> None:
    reg = ScraperRegistry()
    assert reg.sites() == ["dailymotion", "youtube"]
    assert reg.get("YouTube").site == "youtube"
    with pytest.raises(UnknownSite, match="vimeo"):
        reg.get("vimeo")


def test_prefix_store(tmp_path: Path) -> None:
    store = PrefixStore(tmp_path / "prefixes.toml")
    assert store.all() == {}
    store.add("dailymotion", "https://geo.dailymotion.com/player/")
    store.add("youtube", "https://yt.example/")
    with pytest.raises(DuplicatePrefix):
        store.add("dailymotion", "https://geo.dailymotion.com/player/")
    with pytest.raises(SettingsError):
        store.add("dailymotion", "geo.dailymotion.com")
    again = PrefixStore(tmp_path / "prefixes.toml")
    assert again.for_site("dailymotion") == ["https://geo.dailymotion.com/player/"]
    assert again.all("youtube") == {"youtube": ["https://yt.example/"]}
    again.remove("youtube", "https://yt.example/")
    assert again.for_site("youtube") == []
    with pytest.raises(PrefixNotFound):
        again.remove("youtube", "https://yt.example/")
    (tmp_path / "prefixes.toml").write_text("[[[", encoding="utf-8")
    with pytest.raises(SettingsError):
        again.all()


def test_scrape_workflow_reports_and_remembers_referer(
    repo: LibraryRepository, tmp_path: Path
) -> None:
    fetcher = FakePageFetcher({"https://site/a": fixture_page("dailymotion_article.html")})
    reporter = RecordingReporter()
    prefixes = PrefixStore(tmp_path / "p.toml")
    prefixes.add("dailymotion", "https://players.example-cdn.com/")
    found = ScrapeWorkflow(ScraperRegistry(), fetcher, prefixes, repo, reporter).run(
        "dailymotion", "https://site/a"
    )
    assert len(found) == 5
    assert reporter.texts("step") == [
        "Fetching https://site/a",
        "Looking for 7 Dailymotion link patterns",
    ]
    assert repo.referer_for(DM + "x8abc12") == "https://site/a"


def test_scrape_workflow_no_results(tmp_path: Path) -> None:
    fetcher = FakePageFetcher({"u": fixture_page("no_videos.html")})
    wf = ScrapeWorkflow(
        ScraperRegistry(), fetcher, PrefixStore(tmp_path / "p"), None, RecordingReporter()
    )
    with pytest.raises(NoVideosFound) as exc:
        wf.run("youtube", "u")
    assert "JavaScript" in exc.value.hint


def _fetcher(handler: object) -> HttpxFetcher:
    return HttpxFetcher(client=httpx.Client(transport=httpx.MockTransport(handler)))  # type: ignore[arg-type]


def test_httpx_fetcher() -> None:
    ok = _fetcher(lambda req: httpx.Response(200, text="<html>hi</html>"))
    assert ok.fetch("https://example.com") == "<html>hi</html>"
    with pytest.raises(FetchFailed, match="HTTP 403"):
        _fetcher(lambda req: httpx.Response(403)).fetch("https://example.com")
    with pytest.raises(FetchFailed, match="not a web address"):
        ok.fetch("example.com")

    def boom(req: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("nope")

    with pytest.raises(FetchFailed, match="Could not fetch"):
        _fetcher(boom).fetch("https://example.com")
