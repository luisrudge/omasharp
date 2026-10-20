---
name: verify-omasharp
description: Prove a change to Omasharp (the omarchy-app template, its bridge, the agent skill, the analyzers) works the way users meet it. Pack this checkout into a private template store, generate a window app or panel with dotnet new omarchy-app, drive it with the app's own check.sh (private compositor, --keys, visible text, screenshot), and keep the evidence. Use after changing template/, analyzers/ or pack/, before saying a change works, or to reproduce a reported bug in a fresh app.
---

# Verify Omasharp

Users touch Omasharp through `dotnet new omarchy-app`, the app it generates (`Backend.cs`, `Main.qml`, `bridge/`), the app's skill and `check.sh`, and the build targets (`-t:Install`, `-t:Package`, `-t:UpdateBridge`). Verify a change there, in an app generated from this checkout, never by editing files in `template/` and calling that proof.

`sh scripts/test.sh` is the full regression suite (what CI runs) and must still pass before you're done. This skill is for proving the behavior your change is about, and for reproducing bugs, in minutes rather than the suite's full run.

Feature map: [features/README.md](features/README.md). Read it, then use the matching feature file as the recipe.

## Launch

```sh
S=.claude/skills/verify-omasharp/scripts/run.sh     # from the repo root
RUN=$(sh $S setup)                                  # about a minute: packs the analyzers and the template
APP=$(sh $S new "$RUN" Tally)                       # or: sh $S new "$RUN" Counter --kind panel
cd "$APP" && sh .agents/skills/omasharp/scripts/check.sh
```

`setup` makes `RUN` (`${TMPDIR:-/tmp}/omasharp-verify.XXXXXX`) and puts these in it:
- `hive/`: a private template store with Omasharp.Templates from this checkout;
- `pkg/`: Omasharp.Analyzers at a version for this run only (`analyzers-version`);
- `NuGet.config` and `Directory.Build.props`, so apps made in `RUN` use that package (nuget.org stays for everything else);
- `data/` and `home/`, stand-ins for `XDG_DATA_HOME` and `HOME`.

It's ready when it prints the path. `new` prints the app's folder. The app is usable when its `check.sh` ends with `Checked the dev build.` and its visible text shows `Nothing counted yet` (window) or the panel's buttons.

A run packs the checkout as it was at `setup`. After you change `template/`, `analyzers/` or `pack/`, make a new run: `doctor` says when it's stale.

## Doctor

```sh
sh $S doctor "$RUN"
```

Read-only. It checks that the run's package matches `template/bridge/VERSION`, that template/analyzers/pack haven't changed since `setup`, that `omarchy-app` is in the run's hive, and that the build tools are present. It also says whether a Hyprland session is there (if not, only `check.sh --offscreen` works: window apps, visible text, no screenshot, keys or panels). Run it first whenever a result looks off. Exit code 0 means drive it.

## Drive

The harness is the generated app's own `check.sh` (`.agents/skills/omasharp/scripts/check.sh`, from `template/` in this checkout). Run it in the app's folder:

- `check.sh`: unit tests, dev build, then a run in a private nested Hyprland you never see. It prints every visible text top to bottom (`obj/shot.txt`) and saves `obj/shot.png`. It fails if the app didn't start, died, or its QML threw.
- `--keys '{Ctrl+Return}text{Tab}{Wait:500}'`: type into the app first (`keys.py`; keys stay inside the private compositor).
- `--release`: the native AOT publish instead (fails on IL2xxx/IL3xxx warnings). A change isn't verified until the release build shows the same values as the dev build.
- `--offscreen`: Qt's offscreen platform, no compositor (window apps only; what CI uses).
- `--env KEY=VALUE`, `--args`, `--size WxH`, `--float`, `--seed DIR`, `--no-tests`: see `check.sh --help`.

Assert on `obj/shot.txt` lines (`Label: Counted 2 times`, `Button: Count`), not on pixels, unless the change is about drawing. Then open `obj/shot.png` and look.

To exercise the change, edit the generated app (`Backend.cs`, `Main.qml`) the way a user or their agent would, following `template/.agents/skills/omasharp/SKILL.md`, then rerun `check.sh`. Panels: the backend's protocol can also be driven directly (features/panel-app.md).

Rules that keep this off the owner's desktop (AGENTS.md):
- Windows go through `check.sh` only, never `dotnet run`, which opens a real window.
- Never add a monitor or send keys to the user's session.
- Install and release commands run with `HOME="$RUN/home"` (features/install-and-release.md).

## Evidence

```sh
sh $S keep "$RUN" "$APP" window-count     # prints ${TMPDIR:-/tmp}/omasharp-verify-evidence/<run>/window-count/
```

`keep` copies the app's last check: `shot.png`, `shot.txt`, `run.log` (the app's output) and `build.log`. Run it after each drive you want as proof, with a label per feature and step. For commands with no check.sh (the stdio protocol, install, UpdateBridge), save their output to the same folder with `tee`.

Proof standards:
- Drive the user's path: `dotnet new`, then the app's `Backend.cs`/`Main.qml`, and `check.sh` or the documented build targets. Never use internal hooks or files in `template/` in place of a generated app.
- Capture the action (the `--keys`, the edit, the command) and the result (the visible-text lines, the protocol reply), not only the final screenshot.
- Check side effects next to what's visible: files the app wrote (in `obj/omasharp/xdg/` under check.sh, `$RUN/home` for installs), files `UpdateBridge` kept or replaced, what `-t:Install` put in `$RUN/home/.local`.
- Prove both builds: dev and `--release`.
- Note whether a result came from the private compositor or `--offscreen`. Offscreen proves visible text, not keys or pixels.

## Cleanup

```sh
sh $S cleanup "$RUN"
```

Removes `RUN` (apps, hive, packages, the stand-in HOME) and the run's analyzers version from the NuGet cache. The evidence folder stays. check.sh stops its own compositor and app on every exit. If one was interrupted, find leftovers with `pgrep -af "omasharp-verify"` and kill only those PIDs, never by name pattern. The host cache in `~/.cache/omasharp/` is shared with every app and stays.

## Helpers

`scripts/run.sh`, executable, with `--help`:

```
run.sh setup                 pack the analyzers and the template into a new run folder; prints its path
run.sh new RUN NAME [ARGS]   dotnet new omarchy-app -n NAME [ARGS] in RUN (e.g. --kind panel); prints the app's path
run.sh doctor RUN            is RUN worth driving: packed from the checkout as it is now, tools present
run.sh keep RUN APP LABEL    copy APP's last check (obj/shot.png, obj/shot.txt, logs) to the evidence folder
run.sh cleanup RUN           remove RUN and its analyzers package from the NuGet cache; evidence stays
```

Several runs can exist side by side; each has its own hive, package version and apps. check.sh's compositor is per run of check.sh, so two checks at once don't share a screen.
