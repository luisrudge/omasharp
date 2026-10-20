# Panel app

`dotnet new omarchy-app -n <Name> --kind panel` makes an omarchy-shell panel. The same counter `Backend.cs` runs as its own process and talks to the panel's QML in JSON lines over stdin/stdout, so a crash there can't take the shell down.

## Sub-features

- `pan-new`: the app has `plugin/manifest.json`, `plugin/Panel.qml`, and no release workflow.
- `pan-dev`: in dev mode the panel opens in its own Quickshell (the dev shell) with the shell's controls, showing the label and the Count, Reset and Close buttons.
- `pan-count`: Ctrl+Return in the panel counts.
- `pan-protocol`: the published backend (`plugin/bin/<Name>`) answers `hello`, `set`, `call` and `get`.
- `pan-shim`: the build generates `plugin/Backend.qml` with one property per C# property.

## How to get to it (user POV)

- `dotnet new omarchy-app -n Counter --kind panel`.
- `dotnet publish -t:Install`, then `omarchy-shell shell toggle <id>` (users; installs into the real shell, never do it while verifying).
- `sh .agents/skills/omasharp/scripts/check.sh` in the app folder.

## Driving it with check.sh

Preconditions:

- Baseline run; doctor exits 0 with `ok Hyprland` and no `quickshell not found` note. Omarchy's shell modules (`qs.Commons`, `qs.Ui`) are installed.

- **New panel.** Run `PAN=$(sh $S new "$RUN" Counter --kind panel) && ls "$PAN/plugin" && ls -a "$PAN"`. `plugin/` has `manifest.json` and `Panel.qml`, and there is no `.github/`.
- **Dev shell.** Run `cd "$PAN" && sh .agents/skills/omasharp/scripts/check.sh`. The visible text has `Text: Nothing counted yet`, `Button: Count`, `Button: Reset` and `Button: Close`. Exit code 0.
- **Count.** Run `sh .agents/skills/omasharp/scripts/check.sh --no-tests --keys '{Ctrl+Return}'`. The visible text is `Text: Counted 1 time`. Keep it: `sh $S keep "$RUN" "$PAN" pan-count`.
- **Generated Backend.qml.** Run `grep -E 'property (int|string)' plugin/Backend.qml`. It lists `count` and `label`.
- **Protocol.** Run `XDG_DATA_HOME="$RUN/data" dotnet publish -v quiet -nologo`, then `(printf '%s\n' '{"op":"call","id":1,"name":"increment","args":[]}' '{"op":"get","id":2,"name":"label"}'; sleep 0.5) | XDG_DATA_HOME="$RUN/data" plugin/bin/Counter | tee "$EVIDENCE/pan-protocol.txt"`. The first line is `{"op":"hello","props":{"count":0,"label":"Nothing counted yet"},"writable":["count"],"methods":["increment","reset"]}`, then `notify` for `count` 1 and `label` `Counted 1 time`, `{"op":"result","id":1,"value":null}` and `{"op":"result","id":2,"value":"Counted 1 time"}`.
- **Set.** Send `{"op":"set","name":"count","value":5}`. The reply has `{"op":"notify","name":"count","value":5}` (the value C# kept).

## Gotchas

- Panels can't use `--offscreen` (check.sh refuses), so without Hyprland only `pan-protocol` and `pan-shim` are reachable.
- `dotnet build` of a panel runs `Backend`'s constructor (to generate `Backend.qml`). Give raw builds `XDG_DATA_HOME="$RUN/data"`, or the app's constructor writes to the user's real data folder.
- The backend exits when stdin closes. Keep the pipe open (`sleep`) long enough to read the replies.
- stdout carries only the protocol. The backend's `Console.WriteLine` goes to stderr.
- `$EVIDENCE` is the folder `sh $S keep` printed, or any folder under `${TMPDIR:-/tmp}/omasharp-verify-evidence/`.
