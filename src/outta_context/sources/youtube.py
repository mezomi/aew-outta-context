from __future__ import annotations

import re

from outta_context.sources.base import VideoSource

_ID = r"([\w-]{6,})"
_HOST = r"^(?:https?:)?//(?:www\.|m\.|music\.)?"


class YouTubeSource(VideoSource):
    name = "youtube"
    label = "YouTube"
    patterns = (
        re.compile(_HOST + rf"youtube\.com/watch\?(?:.*&)?v={_ID}", re.I),
        re.compile(
            _HOST + rf"youtube(?:-nocookie)?\.com/(?:embed|shorts|live|v)/(?!videoseries){_ID}",
            re.I,
        ),
        re.compile(rf"^(?:https?:)?//(?:www\.)?youtu\.be/{_ID}", re.I),
    )

    def url_for_id(self, video_id: str) -> str:
        return f"https://www.youtube.com/watch?v={video_id}"
