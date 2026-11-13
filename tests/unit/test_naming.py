import pytest

from outta_context.core.errors import InvalidTitle
from outta_context.core.naming import clean_title, safe_filename


@pytest.mark.parametrize(
    ("title", "expected"),
    [
        ("He said what", "He said what"),
        ('a<b>c:d"e/f\\g|h?i*j', "a_b_c_d_e_f_g_h_i_j"),
        ("ends with dots...", "ends with dots"),
        ("  spaced   out  ", "spaced out"),
        ("CON", "CON_"),
        ("com1.txt", "com1.txt_"),
        ("tab\there", "tab here"),
        ("emoji 🔥 ok", "emoji 🔥 ok"),
    ],
)
def test_safe_filename(title: str, expected: str) -> None:
    assert safe_filename(title) == expected


@pytest.mark.parametrize("title", ["", "   ", "???", "..."])
def test_unusable_titles(title: str) -> None:
    with pytest.raises(InvalidTitle):
        safe_filename(title)


def test_long_title_is_cut() -> None:
    name = safe_filename("x" * 500)
    assert len(name) == 120
    assert len(safe_filename("a" * 119 + " " + "b" * 50)) <= 120


def test_clean_title() -> None:
    assert clean_title(" Table   spot ") == "Table spot"
