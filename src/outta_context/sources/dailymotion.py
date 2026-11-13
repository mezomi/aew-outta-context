from __future__ import annotations

import re

from outta_context.sources.base import VideoSource

_ID = r"([a-zA-Z0-9]{5,})"
_HOST = r"^(?:https?:)?//(?:www\.|m\.)?"


class DailymotionSource(VideoSource):
    name = "dailymotion"
    label = "Dailymotion"
    patterns = (
        re.compile(_HOST + rf"dailymotion\.com/(?:embed/)?video/{_ID}", re.I),
        re.compile(
            rf"^(?:https?:)?//geo\.dailymotion\.com/player(?:/[\w-]+)?\.html\?(?:.*&)?video={_ID}",
            re.I,
        ),
        re.compile(_HOST + rf"dailymotion\.com/player\.html\?(?:.*&)?video={_ID}", re.I),
        re.compile(rf"^(?:https?:)?//(?:www\.)?dai\.ly/{_ID}", re.I),
    )

    def url_for_id(self, video_id: str) -> str:
        return f"https://www.dailymotion.com/video/{video_id}"
