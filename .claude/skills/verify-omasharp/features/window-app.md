# Window app

`dotnet new omarchy-app -n <Name>` makes a window app: a counter whose `Backend.cs` QML sees as `backend`, in a window that follows the Omarchy theme. It builds in dev mode (JIT, QML from the folder) and as a native AOT release, and both show the same values.

## Sub-features

- `win-new`: the app has the files agents need (AGENTS.md, CLAUDE.md, the skill, bridge/MANIFEST, the release workflow).
- `win-dev`: the dev build starts and shows `Nothing counted yet`.
- `win-count`: Count and Reset (and Ctrl+Return / Ctrl+R) change the label through `backend`.
- `win-release`: the native AOT build shows the same text, with no IL warnings.
- `win-theme`: colors come from the current Omarchy theme.
- `win-app-id`: `--app-id` sets the Wayland app id and `<AppId>`.

## How to get to it (user POV)

- `dotnet new omarchy-app -n Tally` (optionally `--app-id tally-app`).
- `dotnet run` opens it (users only; agents use check.sh).
- `sh .agents/skills/omasharp/scripts/check.sh [--release]` in the app folder.

## Driving it with check.sh

Preconditions:

- Baseline run; doctor exits 0 with `ok Hyprland` (else use `--offscreen` and skip keys and screenshots).

- **New app.** Run `APP=$(sh $S new "$RUN" Tally) && ls -a "$APP" "$APP/.agents/skills/omasharp"`. The folder has `Backend.cs`, `Main.qml`, `Tally.csproj`, `AGENTS.md`, `CLAUDE.md`, `bridge/`, `tests/`, `.github/workflows/release.yml`, and the skill's `SKILL.md`, `references/` and `scripts/check.sh`.
- **Dev build.** Run `cd "$APP" && sh .agents/skills/omasharp/scripts/check.sh`. The output has `all passed` (unit tests), `== run (private compositor)`, and the visible text `Label: Nothing counted yet`, `AppButton: Count`, `AppButton: Reset`. Exit code 0.
- **Count.** Run `sh .agents/skills/omasharp/scripts/check.sh --no-tests --keys '{Ctrl+Return}{Ctrl+Return}'`. The visible text is `Label: Counted 2 times`. Keep it: `sh $S keep "$RUN" "$APP" win-count`.
- **Reset.** Run `sh .agents/skills/omasharp/scripts/check.sh --no-tests --keys '{Ctrl+Return}{Ctrl+r}'`. The visible text is back to `Label: Nothing counted yet`.
- **Release.** Run `sh .agents/skills/omasharp/scripts/check.sh --release --no-tests --keys '{Ctrl+Return}'`. The output has `== release build (native AOT)`, `Label: Counted 1 time`, no `warning IL` lines, and ends with `Checked the release (native AOT) build.`
- **Theme.** Open `obj/shot.png`. The background and the Count button use the current theme's background and accent (`~/.config/omarchy/current/theme/colors.toml`).
- **App id.** Run `APP2=$(sh $S new "$RUN" Named --app-id named-app) && grep '<AppId>' "$APP2/Named.csproj"`. It prints `<AppId>named-app</AppId>`.

## Gotchas

- Keys need the private compositor: with `--offscreen`, check.sh refuses `--keys`.
- Plain keys reach a focused text field first. The template's shortcuts are Ctrl+... for that reason.
- The first build on a machine compiles the Qt host (`~/.cache/omasharp/host-*`). It's slow once, then cached.
- `dotnet run` opens a real window on the user's screen: never use it to verify.
- The release build caches the host and the AOT output in `obj/`. After editing the bridge in `template/`, make a new run and a new app instead of rebuilding the old one.
