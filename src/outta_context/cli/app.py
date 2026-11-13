"""Root `outta` app: global options and the one place errors are printed."""

from __future__ import annotations

import contextlib
import sys
from typing import Any

import typer
from rich.console import Console
from rich.markup import escape
from typer.core import TyperGroup

from outta_context import __version__
from outta_context.cli import context
from outta_context.core.errors import OuttaError
from outta_context.core.reporter import Verbosity


class OuttaGroup(TyperGroup):
    """Catches OuttaError from any command: prints `error:` and `hint:`, exits with code 1."""

    def invoke(self, ctx: Any) -> Any:
        try:
            return super().invoke(ctx)
        except OuttaError as exc:
            _print_error(ctx, exc.message, exc.hint, exc)
            raise typer.Exit(1) from None
        except KeyboardInterrupt:
            Console(stderr=True).print("[yellow]Cancelled.[/]")
            raise typer.Exit(130) from None
        except Exception as exc:
            if type(exc).__module__.startswith(("click", "typer")):
                raise  # usage errors, --help, typer.Exit: Click prints these itself

            _print_error(
                ctx,
                f"Unexpected problem: {type(exc).__name__}: {exc}",
                "Rerun with --verbose for details.",
                exc,
            )
            raise typer.Exit(1) from None


def _print_error(ctx: Any, message: str, hint: str, exc: BaseException) -> None:
    console = Console(stderr=True, highlight=False)
    services = ctx.find_object(context.AppServices)
    if services is not None and services.verbose:
        console.print_exception(show_locals=False)
    console.print(f"[bold red]error:[/] {escape(message)}")
    if hint:
        console.print(f"[yellow]hint:[/] {escape(hint)}")


app = typer.Typer(
    name="outta",
    cls=OuttaGroup,
    help="Build the AEW Outta Context series: scrape → clip → episode → build → upload.",
    no_args_is_help=True,
    add_completion=False,
    rich_markup_mode="rich",
    context_settings={"help_option_names": ["-h", "--help"]},
    pretty_exceptions_enable=False,
)


def _version(value: bool) -> None:
    if value:
        typer.echo(f"outta {__version__}")
        raise typer.Exit()


@app.callback()
def root(
    ctx: typer.Context,
    verbose: bool = typer.Option(False, "--verbose", "-v", help="Show details and tracebacks."),
    quiet: bool = typer.Option(False, "--quiet", "-q", help="Only show results and errors."),
    version: bool = typer.Option(
        False, "--version", callback=_version, is_eager=True, help="Show the version and exit."
    ),
) -> None:
    if verbose and quiet:
        raise typer.BadParameter("Use either --verbose or --quiet, not both.")
    level = Verbosity.VERBOSE if verbose else Verbosity.QUIET if quiet else Verbosity.NORMAL
    ctx.obj = context.services_factory(level)


def _register() -> None:
    from outta_context.cli import clip, config, doctor, episode, scrape, storage, upload

    app.command("clip", no_args_is_help=True)(clip.clip)
    app.add_typer(scrape.build_app(), name="scrape")
    app.add_typer(episode.app, name="episode")
    app.add_typer(storage.app, name="storage")
    app.command("upload", no_args_is_help=True)(upload.upload)
    app.add_typer(config.app, name="config")
    app.command("doctor")(doctor.doctor)


_register()


def main() -> None:
    for stream in (sys.stdout, sys.stderr):
        reconfigure = getattr(stream, "reconfigure", None)
        if reconfigure is not None:
            with contextlib.suppress(ValueError, OSError):
                reconfigure(errors="replace")
    app()
