"""Ffmpeg wraps ffmpeg/ffprobe: probe, normalize and concat. Only module that runs them."""

from __future__ import annotations

import json
import shutil
import subprocess
from collections.abc import Callable, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from outta_context.core.errors import FfmpegFailed, FfmpegMissing
from outta_context.media.models import NormalizeTarget

Runner = Callable[..., "subprocess.CompletedProcess[str]"]


@dataclass(frozen=True)
class ProbeResult:
    duration: float
    width: int | None
    height: int | None
    fps: float | None
    has_audio: bool


def _parse_rate(rate: str | None) -> float | None:
    if not rate or rate == "0/0":
        return None
    if "/" in rate:
        num, den = rate.split("/", 1)
        return float(num) / float(den) if float(den) else None
    return float(rate)


class Ffmpeg:
    def __init__(
        self,
        runner: Runner = subprocess.run,
        which: Callable[[str], str | None] = shutil.which,
        ffmpeg: str = "ffmpeg",
        ffprobe: str = "ffprobe",
        detail: Callable[[str], None] | None = None,
    ) -> None:
        self._runner = runner
        self._which = which
        self.ffmpeg = ffmpeg
        self.ffprobe = ffprobe
        self._detail = detail

    def available(self) -> bool:
        return self._which(self.ffmpeg) is not None and self._which(self.ffprobe) is not None

    def ensure_available(self) -> None:
        missing = [exe for exe in (self.ffmpeg, self.ffprobe) if self._which(exe) is None]
        if missing:
            raise FfmpegMissing(f"{' and '.join(missing)} not found on PATH.")

    def version(self) -> str | None:
        if self._which(self.ffmpeg) is None:
            return None
        result = self._run([self.ffmpeg, "-version"], check=False)
        first = (result.stdout or "").splitlines()
        return first[0] if first else None

    def _run(self, cmd: Sequence[str], check: bool = True) -> subprocess.CompletedProcess[str]:
        if self._detail:
            self._detail("$ " + subprocess.list2cmdline(list(cmd)))
        try:
            result = self._runner(
                list(cmd), capture_output=True, text=True, encoding="utf-8", errors="replace"
            )
        except FileNotFoundError as exc:
            raise FfmpegMissing(f"{cmd[0]} not found on PATH.") from exc
        if check and result.returncode != 0:
            tail = "\n".join((result.stderr or "").strip().splitlines()[-5:])
            raise FfmpegFailed(f"{Path(cmd[0]).name} failed (exit {result.returncode}): {tail}")
        return result

    # --- probe -------------------------------------------------------------
    def probe_command(self, path: Path) -> list[str]:
        return [
            self.ffprobe,
            "-v",
            "error",
            "-show_entries",
            "format=duration:stream=codec_type,width,height,avg_frame_rate",
            "-of",
            "json",
            str(path),
        ]

    def probe(self, path: Path) -> ProbeResult:
        self.ensure_available()
        result = self._run(self.probe_command(path))
        data: dict[str, Any] = json.loads(result.stdout or "{}")
        streams = data.get("streams", [])
        video: dict[str, Any] = next((s for s in streams if s.get("codec_type") == "video"), {})
        return ProbeResult(
            duration=float(data.get("format", {}).get("duration") or 0.0),
            width=video.get("width"),
            height=video.get("height"),
            fps=_parse_rate(video.get("avg_frame_rate")),
            has_audio=any(s.get("codec_type") == "audio" for s in streams),
        )

    # --- normalize ---------------------------------------------------------
    def normalize_command(
        self, src: Path, dst: Path, target: NormalizeTarget, has_audio: bool = True
    ) -> list[str]:
        w, h = target.width, target.height
        vf = (
            f"scale={w}:{h}:force_original_aspect_ratio=decrease,"
            f"pad={w}:{h}:(ow-iw)/2:(oh-ih)/2:color=black,setsar=1,fps={target.fps},format=yuv420p"
        )
        cmd = [self.ffmpeg, "-hide_banner", "-loglevel", "error", "-y", "-i", str(src)]
        if not has_audio:
            cmd += ["-f", "lavfi", "-i", f"anullsrc=r={target.audio_rate}:cl=stereo"]
        cmd += ["-map", "0:v:0", "-map", "0:a:0" if has_audio else "1:a:0"]
        cmd += ["-vf", vf, "-c:v", target.video_codec, "-preset", target.preset]
        cmd += ["-crf", str(target.crf), "-c:a", target.audio_codec, "-ar", str(target.audio_rate)]
        cmd += ["-ac", "2", "-b:a", "192k"]
        if not has_audio:
            cmd += ["-shortest"]
        cmd += ["-movflags", "+faststart", str(dst)]
        return cmd

    def normalize(self, src: Path, dst: Path, target: NormalizeTarget) -> None:
        self.ensure_available()
        info = self.probe(src)
        dst.parent.mkdir(parents=True, exist_ok=True)
        tmp = dst.with_name(dst.stem + ".part.mp4")
        self._run(self.normalize_command(src, tmp, target, has_audio=info.has_audio))
        tmp.replace(dst)

    # --- concat ------------------------------------------------------------
    @staticmethod
    def concat_list(paths: Sequence[Path]) -> str:
        lines = []
        for path in paths:
            escaped = path.resolve().as_posix().replace("'", "'\\''")
            lines.append(f"file '{escaped}'")
        return "\n".join(lines) + "\n"

    def concat_command(self, list_file: Path, dst: Path) -> list[str]:
        return [
            self.ffmpeg,
            "-hide_banner",
            "-loglevel",
            "error",
            "-y",
            "-f",
            "concat",
            "-safe",
            "0",
            "-i",
            str(list_file),
            "-c",
            "copy",
            "-movflags",
            "+faststart",
            str(dst),
        ]

    def concat(self, paths: Sequence[Path], dst: Path, workdir: Path) -> None:
        self.ensure_available()
        workdir.mkdir(parents=True, exist_ok=True)
        dst.parent.mkdir(parents=True, exist_ok=True)
        list_file = workdir / "concat.txt"
        list_file.write_text(self.concat_list(paths), encoding="utf-8")
        tmp = dst.with_name(dst.stem + ".part.mp4")
        self._run(self.concat_command(list_file, tmp))
        tmp.replace(dst)
