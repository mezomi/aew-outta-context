import pytest

from outta_context.core.errors import UnsupportedUrl
from outta_context.sources.registry import SourceRegistry

DM = "https://www.dailymotion.com/video/"
YT = "https://www.youtube.com/watch?v="


@pytest.mark.parametrize(
    ("url", "site", "canonical"),
    [
        (
            "https://www.dailymotion.com/video/k1ojSwOaH7psOtMbpGS",
            "dailymotion",
            DM + "k1ojSwOaH7psOtMbpGS",
        ),
        ("dailymotion.com/video/x8abc12", None, None),
        (
            "https://www.dailymotion.com/embed/video/x8abc12?autoplay=1",
            "dailymotion",
            DM + "x8abc12",
        ),
        (
            "https://geo.dailymotion.com/player/x9abc.html?video=k1ojSwOaH7psOtMbpGS",
            "dailymotion",
            DM + "k1ojSwOaH7psOtMbpGS",
        ),
        (
            "https://geo.dailymotion.com/player.html?mute=1&video=x8abc12",
            "dailymotion",
            DM + "x8abc12",
        ),
        ("https://dai.ly/x8abc12", "dailymotion", DM + "x8abc12"),
        ("//www.dailymotion.com/video/x8abc12", "dailymotion", DM + "x8abc12"),
        ("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "youtube", YT + "dQw4w9WgXcQ"),
        (
            "https://www.youtube.com/watch?feature=share&v=dQw4w9WgXcQ&t=10",
            "youtube",
            YT + "dQw4w9WgXcQ",
        ),
        ("https://m.youtube.com/watch?v=dQw4w9WgXcQ", "youtube", YT + "dQw4w9WgXcQ"),
        ("https://youtu.be/dQw4w9WgXcQ?si=xyz", "youtube", YT + "dQw4w9WgXcQ"),
        ("https://youtu.be/abc123XYZ", "youtube", YT + "abc123XYZ"),
        ("https://www.youtube.com/shorts/aqz-KE-bpKQ", "youtube", YT + "aqz-KE-bpKQ"),
        ("https://www.youtube.com/embed/dQw4w9WgXcQ", "youtube", YT + "dQw4w9WgXcQ"),
        ("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ", "youtube", YT + "dQw4w9WgXcQ"),
        ("https://www.youtube.com/live/dQw4w9WgXcQ", "youtube", YT + "dQw4w9WgXcQ"),
    ],
)
def test_detect(url: str, site: str | None, canonical: str | None) -> None:
    registry = SourceRegistry()
    if site is None:
        with pytest.raises(UnsupportedUrl):
            registry.detect(url)
        return
    source = registry.detect(url)
    assert source.name == site
    assert source.canonical_url(url) == canonical


@pytest.mark.parametrize(
    "url",
    [
        "https://example.com/video/x8abc12",
        "https://vimeo.com/12345",
        "not a url",
        "https://www.youtube.com/embed/videoseries?list=PL1",
        "https://www.youtube.com/@AEW",
    ],
)
def test_unsupported(url: str) -> None:
    with pytest.raises(UnsupportedUrl) as exc:
        SourceRegistry().detect(url)
    assert "outta scrape" in exc.value.hint


def test_get_and_canonical_error() -> None:
    reg = SourceRegistry()
    assert reg.get("youtube").label == "YouTube"
    with pytest.raises(KeyError):
        reg.get("vimeo")
    with pytest.raises(ValueError, match="not a YouTube link"):
        reg.get("youtube").canonical_url("https://dai.ly/x8abc12")
