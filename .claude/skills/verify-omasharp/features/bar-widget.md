# Bar widget app

`dotnet new omarchy-app -n <Name> --kind bar-widget` makes an icon in the Omarchy bar that opens a dropdown. The icon shows the counter's count, and the dropdown has the label and the Count and Reset buttons. The backend runs as its own process, once per session, shared by every monitor's icon, and talks JSON lines over stdin/stdout like a panel's.

## Sub-features

- `bar-new`: the app has `plugin/manifest.json` (kinds `bar-widget` and `service`), `plugin/BarWidget.qml`, `plugin/Service.qml`, and no `Panel.qml` or release workflow.
- `bar-dev`: in dev mode the widget sits in a stand-in bar at the top of the screen with its dropdown open, showing the count on the icon and `Nothing counted yet` in the dropdown.
- `bar-count`: Return in the dropdown counts; the icon and the label both change.
- `bar-vertical`: with the bar on the left edge, the icon sits at the bottom of the strip and the dropdown opens beside it.
- `bar-protocol`: the published backend (`plugin/bin/<Name>`) answers the same protocol as a panel's.

## How to get to it (user POV)

- `dotnet new omarchy-app -n Tally --kind bar-widget`.
- `dotnet publish -t:Install` puts the icon in the bar, on the right; `omarchy bar move <id> --section left` moves it (users; installs into the real shell, never do it while verifying).
- `sh .agents/skills/omasharp/scripts/check.sh` in the app folder.

## Driving it with check.sh

Preconditions:

- Baseline run; doctor exits 0 with `ok Hyprland` and no `quickshell not found` note. Omarchy's shell modules (`qs.Commons`, `qs.Ui`) are installed.

- **New bar widget.** Run `BAR=$(sh $S new "$RUN" Tally --kind bar-widget) && ls "$BAR/plugin" && ls -a "$BAR"`. `plugin/` has `BarWidget.qml`, `Service.qml` and `manifest.json`, and there is no `.github/`. `python3 -I -c 'import json,sys; m=json.load(open(sys.argv[1])); print(m["kinds"], m["entryPoints"])' "$BAR/plugin/manifest.json"` prints `['bar-widget', 'service']` and both entry points.
- **Dev bar.** Run `cd "$BAR" && sh .agents/skills/omasharp/scripts/check.sh`. The visible text starts with `BarIconButton: 0`, then `Text: Nothing counted yet`, `Button: Count` and `Button: Reset`. Exit code 0. `obj/shot.png` shows the strip along the top with `0` at its right end and the dropdown under it.
- **Count.** Run `sh .agents/skills/omasharp/scripts/check.sh --no-tests --keys '{Enter}'`. The visible text has `BarIconButton: 1` and `Text: Counted 1 time`. Keep it: `sh $S keep "$RUN" "$BAR" bar-count`.
- **Vertical bar.** Run `sh .agents/skills/omasharp/scripts/check.sh --no-tests --env OMASHARP_BAR_POSITION=left`. Exit code 0; `obj/shot.png` shows a strip on the left edge with the icon at its bottom and the dropdown beside it. Keep it: `sh $S keep "$RUN" "$BAR" bar-vertical`.
- **Protocol.** As in [panel-app.md](./panel-app.md)'s Protocol step, with `plugin/bin/Tally`.

## Gotchas

- Escape closes the dropdown, which ends the run: don't end `--keys` with `{Esc}`.
- The visible text lists `OpticalGlyph`, `Text` and `TextMetrics` lines under `BarIconButton`; they repeat the icon's text.
- check.sh shows one screen with no gaps, so the dropdown touches the bar. One backend shared by several monitors, and placement in the real bar, are only visible in the real shell, which this skill never touches.
- `dotnet build` runs `Backend`'s constructor, as for panels: give raw builds `XDG_DATA_HOME="$RUN/data"`.
