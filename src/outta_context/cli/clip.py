from __future__ import annotations

import typer

from outta_context.cli.context import get_services
from outta_context.workflows.make_clip import ClipRequest


def clip(
    ctx: typer.Context,
    url: str = typer.Argument(..., help="Dailymotion or YouTube video link."),
    range_: str = typer.Option(
        ..., "--range", "-r", help="START-END: 90-112, 1:30-1:52 or 1:02:10-1:02:30."
    ),
    title: str = typer.Option(
        ..., "--title", "-t", help="Clip title (required, unique). Becomes the file name."
    ),
    episode: str | None = typer.Option(None, "--episode", "-e", help="Add to this episode."),
    no_episode: bool = typer.Option(False, "--no-episode", help="Don't add to any episode."),
    upload: bool = typer.Option(False, "--upload", help="Upload to the default storage after."),
    to: str | None = typer.Option(None, "--to", help="Storage for --upload (default: default)."),
    referer: str | None = typer.Option(
        None, "--referer", help="Page the video is embedded on (for embed-only videos)."
    ),
) -> None:
    """Download a titled clip (Dailymotion or YouTube) and add it to the current episode."""
    if episode and no_episode:
        raise typer.BadParameter("Use --episode or --no-episode, not both.")
    services = get_services(ctx.obj)
    services.make_clip_workflow().run(
        ClipRequest(
            url=url,
            range_text=range_,
            title=title,
            episode=episode,
            no_episode=no_episode,
            upload=upload or to is not None,
            referer=referer,
            storage=to,
        )
    )
