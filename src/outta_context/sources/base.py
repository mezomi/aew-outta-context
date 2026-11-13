"""Sites we can download clips from."""

from __future__ import annotations

import abc
import re


class VideoSource(abc.ABC):
    """One video site. Subclasses list URL patterns whose first group is the video ID."""

    name: str  # short key, e.g. "youtube"
    label: str  # shown to the user, e.g. "YouTube"
    patterns: tuple[re.Pattern[str], ...]

    def video_id(self, url: str) -> str | None:
        for pattern in self.patterns:
            match = pattern.search(url.strip())
            if match:
                return match.group(1)
        return None

    def matches(self, url: str) -> bool:
        return self.video_id(url) is not None

    @abc.abstractmethod
    def url_for_id(self, video_id: str) -> str:
        """The canonical watch URL for a video ID."""

    def canonical_url(self, url: str) -> str:
        video_id = self.video_id(url)
        if video_id is None:
            raise ValueError(f"{url} is not a {self.label} link")
        return self.url_for_id(video_id)
