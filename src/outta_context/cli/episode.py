from __future__ import annotations

import json

import typer
from rich.markup import escape
from rich.table import Table

from outta_context.cli.context import get_services
from outta_context.core.timerange import format_time
from outta_context.library.models import EpisodeStatus

app = typer.Typer(help="Group clips into ~10-minute episodes.", no_args_is_help=True)


def _status_dict(s: EpisodeStatus) -> dict[str, object]:
    return {
        "name": s.episode.name,
        "state": s.episode.state,
        "target": s.episode.target_length,
        "total": round(s.total, 2),
        "remaining": round(s.remaining, 2),
        "output": str(s.episode.output_path) if s.episode.output_path else None,
        "clips": [
            {
                "id": c.id,
                "position": c.position,
                "title": c.title,
                "duration": round(c.duration, 2),
                "source": c.source,
                "file": str(c.file_path),
                "exists": c.file_path.exists(),
            }
            for c in s.clips
        ],
    }


def _print_status(ctx: typer.Context, s: EpisodeStatus) -> None:
    services = get_services(ctx.obj)
    console = services.console
    state = "[green]built[/]" if s.episode.is_built else "[cyan]open[/]"
    console.print(f"[bold]{escape(s.episode.name)}[/] ({state})")
    if s.clips:
        table = Table(box=None, pad_edge=False, show_header=True, header_style="dim")
        for col in ("#", "ID", "Title", "Length", "Source"):
            table.add_column(col, justify="right" if col in ("#", "ID", "Length") else "left")
        for c in s.clips:
            title = escape(c.title) + ("" if c.file_path.exists() else " [red](file missing)[/]")
            table.add_row(str(c.position), str(c.id), title, format_time(c.duration), c.source)
        console.print(table)
    else:
        console.print("  [dim]No clips yet.[/]")
    target = format_time(s.episode.target_length)
    line = f"Total {format_time(s.total)} of {target}"
    if s.ready:
        console.print(f"{line} - [green]ready to build[/] (`outta episode build`)")
    else:
        console.print(f"{line} - {format_time(s.remaining)} left")
    if s.episode.output_path:
        console.print(f"Video: {escape(str(s.episode.output_path))}")


@app.command("status")
def status(
    ctx: typer.Context,
    name: str | None = typer.Argument(None, help="Episode name (default: current)."),
    as_json: bool = typer.Option(False, "--json", help="Print as JSON."),
) -> None:
    """Current episode: clips, total time, time left."""
    s = get_services(ctx.obj).episodes.status(name)
    if as_json:
        typer.echo(json.dumps(_status_dict(s), indent=2))
    else:
        _print_status(ctx, s)


@app.command("list")
def list_(ctx: typer.Context, as_json: bool = typer.Option(False, "--json")) -> None:
    """All episodes and their state."""
    services = get_services(ctx.obj)
    statuses = services.episodes.all_statuses()
    if as_json:
        typer.echo(json.dumps([_status_dict(s) for s in statuses], indent=2))
        return
    current = services.episodes.current()
    table = Table(box=None, pad_edge=False, header_style="dim")
    for col in ("Episode", "State", "Clips", "Length", "Video"):
        table.add_column(col)
    for s in statuses:
        mark = " [cyan]← current[/]" if s.episode.id == current.id else ""
        table.add_row(
            escape(s.episode.name) + mark,
            s.episode.state,
            str(len(s.clips)),
            f"{format_time(s.total)} / {format_time(s.episode.target_length)}",
            escape(str(s.episode.output_path or "")),
        )
    services.console.print(table)


@app.command("new", no_args_is_help=True)
def new(ctx: typer.Context, name: str = typer.Argument(..., help='e.g. "Episode 4".')) -> None:
    """Start a new episode by hand; new clips go into it."""
    services = get_services(ctx.obj)
    episode = services.episodes.new(name)
    services.reporter.success(
        f'Started "{episode.name}" ({format_time(episode.target_length)} target)'
    )


@app.command("move", no_args_is_help=True)
def move(
    ctx: typer.Context,
    clip_id: int = typer.Argument(..., help="Clip ID (see `outta episode status`)."),
    position: int = typer.Argument(..., help="New position, starting at 1."),
) -> None:
    """Move a clip to another position in its episode."""
    services = get_services(ctx.obj)
    s = services.episodes.move(clip_id, position)
    services.reporter.success(f"Moved clip {clip_id} to position {position}")
    _print_status(ctx, s)


@app.command("remove", no_args_is_help=True)
def remove(ctx: typer.Context, clip_id: int = typer.Argument(..., help="Clip ID.")) -> None:
    """Take a clip out of its episode (the file is kept)."""
    services = get_services(ctx.obj)
    clip, s = services.episodes.remove(clip_id)
    services.reporter.success(
        f'Removed "{clip.title}" from "{s.episode.name}" (file kept: {clip.file_path})'
    )


@app.command("build")
def build(
    ctx: typer.Context,
    name: str | None = typer.Argument(None, help="Episode name (default: current)."),
    upload: bool = typer.Option(False, "--upload", help="Upload the episode and its clips."),
    to: str | None = typer.Option(None, "--to", help="Storage for --upload."),
    clean_local: bool = typer.Option(
        False, "--clean-local", help="With --upload: delete local clip files once uploaded."
    ),
) -> None:
    """Join the episode's clips into one MP4, then open the next episode."""
    services = get_services(ctx.obj)
    services.build_workflow().run(
        name, upload=upload or to is not None, storage=to, clean_local=clean_local
    )
