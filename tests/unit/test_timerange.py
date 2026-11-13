import pytest

from outta_context.core.errors import InvalidTimeRange, RangeOutsideVideo
from outta_context.core.timerange import format_length, format_time, parse_range, parse_time


@pytest.mark.parametrize(
    ("text", "seconds"),
    [
        ("90", 90),
        ("0", 0),
        ("1:30", 90),
        ("01:05", 65),
        ("1:02:10", 3730),
        ("12.5", 12.5),
        ("1:30.25", 90.25),
        (" 45 ", 45),
    ],
)
def test_parse_time(text: str, seconds: float) -> None:
    assert parse_time(text) == seconds


@pytest.mark.parametrize(
    "text", ["", "abc", "1:", ":30", "1:60", "1:75:00", "1:2:3:4", "-5", "1:a"]
)
def test_parse_time_rejects(text: str) -> None:
    with pytest.raises(InvalidTimeRange):
        parse_time(text)


@pytest.mark.parametrize(
    ("text", "start", "end"),
    [
        ("90-112", 90, 112),
        ("1:30-1:52", 90, 112),
        ("1:02:10-1:02:30", 3730, 3750),
        ("0:45 - 1:05", 45, 65),
    ],
)
def test_parse_range(text: str, start: float, end: float) -> None:
    r = parse_range(text)
    assert (r.start, r.end) == (start, end)
    assert r.length == end - start


@pytest.mark.parametrize("text", ["1:30", "1:52-1:30", "10-10", "1-2-3", "a-b", "-"])
def test_parse_range_rejects(text: str) -> None:
    with pytest.raises(InvalidTimeRange) as exc:
        parse_range(text)
    assert exc.value.hint


def test_end_before_start_message() -> None:
    with pytest.raises(InvalidTimeRange, match="must be after"):
        parse_range("1:52-1:30")


def test_check_within() -> None:
    parse_range("0:10-0:20").check_within(30)
    parse_range("0:10-0:20").check_within(None)
    parse_range("0:10-0:30").check_within(30)
    with pytest.raises(RangeOutsideVideo, match="End 0:40"):
        parse_range("0:10-0:40").check_within(30)
    with pytest.raises(RangeOutsideVideo, match="Start"):
        parse_range("0:35-0:40").check_within(30)


@pytest.mark.parametrize(
    ("seconds", "text"),
    [(0, "0:00"), (65, "1:05"), (600, "10:00"), (3730, "1:02:10"), (-1, "0:00")],
)
def test_format_time(seconds: float, text: str) -> None:
    assert format_time(seconds) == text


def test_format_length_and_str() -> None:
    assert format_length(20) == "20s"
    assert format_length(139) == "2:19"
    assert str(parse_range("45-65")) == "0:45-1:05"
