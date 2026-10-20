# check.sh

Every app's `.agents/skills/omasharp/scripts/check.sh` is how agents see their app. It runs the tests, builds, and runs the app off the user's screen; prints what's visible; keeps the app's data in scratch folders; and fails when the app's QML throws. Agents trust its output, so each promise here is user-facing.

## Sub-features

- `chk-text`: every visible text goes to stdout and `obj/shot.txt`, top to bottom. A password field shows only its masked text.
- `chk-shot`: `obj/shot.png` at 1.5x scale; `--size WxH` (odd sizes rounded down, with a note), `--float`.
- `chk-keys`: `--keys` types into the app inside the private compositor.
- `chk-xdg`: the app gets empty `XDG_DATA_HOME`/`CONFIG`/`STATE`/`CACHE` in `obj/omasharp/xdg/`, never the real ones. `--seed DIR` fills them and `--real-data` turns this off.
- `chk-env`: `--env KEY=VALUE` reaches the app unchanged. Variables that keep it off-screen (`QT_QPA_PLATFORM`, `WAYLAND_DISPLAY`, the XDG dirs, ...) are refused.
- `chk-qml-errors`: a JS error or `Unable to assign` in the app's own QML fails the check.
- `chk-offscreen`: `--offscreen` runs window apps on Qt's offscreen platform (CI).

## How to get to it (user POV)

- `sh .agents/skills/omasharp/scripts/check.sh [options]` in an app folder; `--help` lists the options.

## Driving it with check.sh

Preconditions:

- A fresh window app from the baseline run: `APP=$(sh $S new "$RUN" Probe) && cd "$APP"`.
- `ok Hyprland` from doctor for `chk-shot` and `chk-keys`.

- **Visible text.** Run `sh .agents/skills/omasharp/scripts/check.sh --no-tests`. Lines under `--- visible text, top to bottom` match `obj/shot.txt`.
- **Size.** Run `sh .agents/skills/omasharp/scripts/check.sh --no-tests --size 625x751`. The output says `using 624x750, not 625x751`, and `python3 -I -c 'import struct,sys; print("%dx%d" % struct.unpack(">II", open(sys.argv[1],"rb").read(24)[16:24]))' obj/shot.png` prints `936x1125`.
- **Empty data.** Add to `Backend` a constructor that writes `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "probe", "written.txt")` and a `Seed` property with the trimmed text of `seed.txt` from the same folder (`"none"` if it's missing), shown in `Main.qml` as `"seed: " + backend.seed`. Run `sh .agents/skills/omasharp/scripts/check.sh --no-tests`. The visible text has `seed: none`, `obj/omasharp/xdg/data/probe/written.txt` exists, and nothing new is under `~/.local/share/probe`.
- **Seed.** Run `mkdir -p "$RUN/seed/data/probe" && echo planted > "$RUN/seed/data/probe/seed.txt" && sh .agents/skills/omasharp/scripts/check.sh --no-tests --seed "$RUN/seed"`. The visible text has `seed: planted`, and `$RUN/seed/data/probe/written.txt` doesn't exist.
- **Env.** Add a `Label` showing `Environment.GetEnvironmentVariable("CHECK_ENV")` through a backend property. Run `sh .agents/skills/omasharp/scripts/check.sh --no-tests --env 'CHECK_ENV=a "b" $HOME'`. The label shows `a "b" $HOME` literally. With `--env QT_QPA_PLATFORM=wayland`, the check exits non-zero with `can't set QT_QPA_PLATFORM`.
- **QML error.** Add `Component.onCompleted: notDefinedAnywhere.call()` to the window in `Main.qml`. Run `sh .agents/skills/omasharp/scripts/check.sh --no-tests`. It exits non-zero, printing `--- QML errors ---` and `ReferenceError: notDefinedAnywhere is not defined`.
- **Offscreen.** Run `sh .agents/skills/omasharp/scripts/check.sh --no-tests --offscreen`. The output has `== run (offscreen)` and the same visible text, and makes no screenshot.

## Gotchas

- `obj/omasharp/xdg/` is emptied before every run, so read the app's files right after the check, before the next one.
- `--real-data` hands the app the user's real data: never use it while verifying.
- Visible text is the app's text items, not OCR. A value drawn by `DrawView` appears only as the text `Drawing.Text` emitted.
- `--keys` waits 3 s for the app to start (`OMASHARP_SHOT_WAIT`). An app that starts slower gets the keys before it's ready.
