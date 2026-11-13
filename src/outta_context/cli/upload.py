from __future__ import annotations

from pathlib import Path

import typer

from outta_context.cli.context import get_services


def upload(
    ctx: typer.Context,
    target: str = typer.Argument(..., help="A file path, a clip ID or an episode name."),
    to: str | None = typer.Option(None, "--to", help="Storage name (default: default storage)."),
) -> None:
    """Upload a file, a clip (by ID) or a built episode (by name)."""
    services = get_services(ctx.obj)
    uploader = services.uploader
    path = Path(target).expanduser()
    if path.is_file():
        uploader.upload_file(path, to)
        return
    if target.isdigit():
        uploader.upload_clip(services.repo.get_clip(int(target)), to)
        return
    status = services.episodes.status(target)
    uploader.upload_episode(status.episode, status.clips, to)
