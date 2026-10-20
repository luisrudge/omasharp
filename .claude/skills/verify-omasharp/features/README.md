# Omasharp verification map

The maintained source for verifying what users of Omasharp see: the template, the apps it makes, their `check.sh`, and the build targets. Read this index, then use the matching feature file as the recipe.

## Baseline preconditions

- A run from this checkout: `S=.claude/skills/verify-omasharp/scripts/run.sh; RUN=$(sh $S setup)`, made after your last change to `template/`, `analyzers/` or `pack/`.
- `sh $S doctor "$RUN"` exits 0.
- Recipes that need the private compositor (screenshots, `--keys`, panels) need doctor's `ok Hyprland` line. Without it, run window-app recipes with `--offscreen` and report the rest as not reachable.
- Never drive a run you didn't make, or the user's installed templates or apps.

## Driving conventions

- Start every recipe from a fresh app: `APP=$(sh $S new "$RUN" <Name> [--kind panel])`. Names are per run; pick a new one instead of reusing an app another recipe changed.
- Run `check.sh` from the app's folder: `sh .agents/skills/omasharp/scripts/check.sh`.
- Treat commands as literal. Keep quoted keys and names unchanged.
- Read results from `obj/shot.txt` lines (`<Type>: <text>`, children indented two spaces).
- Builds and runs with HOME-dependent effects (install, release) use `HOME="$RUN/home"`.

## Proof and skip reporting

- Capture the action (keys, edits, commands) and the resulting state, not just the final screen.
- UI proof: `sh $S keep "$RUN" "$APP" <feature>-<step>`, which keeps `shot.txt`, `shot.png` and the logs.
- Command proof (protocol, install, UpdateBridge): the command, its output and its exit code, saved with `tee` into the same evidence folder.
- Side-effect proof: a second, read-only look at the files written (`ls`, `cat`, `sha256sum`).
- Record the feature ID with every artifact, and whether it ran in the private compositor or `--offscreen`.
- Report an unreachable path with the attempted command and the unmet precondition. Never report it as verified through a different path.

## Feature entry contract

Each feature file has an H1 title and one paragraph on the user-visible behavior, then four H2s in this order: `Sub-features`, `How to get to it (user POV)`, `Driving it with check.sh` (or the harness it uses), `Gotchas`. Keep implementation details out; name user paths, commands, visible text and files.

## Features

- [Window app](./window-app.md): `dotnet new omarchy-app`, the counter, dev and release builds, theme.
- [Panel app](./panel-app.md): `--kind panel`, the panel in the dev shell, the backend's stdio protocol.
- [check.sh](./check-sh.md): what agents rely on: visible text, keys, empty XDG dirs, `--seed`, `--env`, failing on QML errors.
- [Install and release](./install-and-release.md): `-t:Install`/`-t:Uninstall`, `-t:Package` and the release's `install.sh`.
- [Bridge update](./update-bridge.md): `-t:UpdateBridge` replaces untouched files and keeps customized ones.

Also worth a quick check after bridge changes: the examples, which build against `template/bridge` directly (no run needed): `cd examples/<name> && sh ../../template/.agents/skills/omasharp/scripts/check.sh` for `hello`, `omacalc` (`--float`), `pomodoro`, `systemmonitor`.
