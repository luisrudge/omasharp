# Bridge update

`dotnet build -t:UpdateBridge` brings a newer bridge and Omasharp skill from the installed template into an app. Files the app never changed are replaced. Customized ones are kept, with the new version saved as `<file>.upstream` and the diff printed. Files added upstream are copied in, and files removed upstream are deleted unless customized.

## Sub-features

- `upd-noop`: on a fresh app nothing changes (`0 updated, 0 added, 0 removed`).
- `upd-kept`: a customized file unchanged upstream is kept, silently merged (`kept … (your customization; unchanged upstream)`).
- `upd-replace`: an untouched file changed upstream is replaced (`updated`).
- `upd-conflict`: a customized file that also changed upstream is kept, with `<file>.upstream` and a diff (`CHANGED`, `to merge by hand`).
- `upd-add-remove`: files added or removed upstream follow, and a removed file the app customized is kept.

## How to get to it (user POV)

- `dotnet build -t:UpdateBridge` in the app folder, after `dotnet new install Omasharp.Templates` brings a newer template.

## Driving it with two runs

Preconditions:

- Run A, from the checkout before the change: `RUN_A=$(sh $S setup)`, and an app made in it: `APP=$(sh $S new "$RUN_A" Tally)`.
- For `upd-replace`, `upd-conflict` and `upd-add-remove`: the bridge change in the checkout, then run B: `RUN_B=$(sh $S setup)`.
- `UpdateBridge` reads the template from the hive named by `OMASHARP_TEMPLATE_HIVE`. Always set it to a run's hive; without it, the update uses the user's installed template.

- **No-op.** Run `cd "$APP" && OMASHARP_TEMPLATE_HIVE=$RUN_A/hive dotnet build -t:UpdateBridge -nologo | grep omasharp`. It prints `omasharp <v> -> <v>` and `0 updated, 0 added, 0 removed, … 0 to merge by hand`.
- **Kept.** Run `echo '// my change' >> bridge/cs/Ui.cs`, then the same update. It prints `kept     bridge/cs/Ui.cs (your customization; unchanged upstream)`, and `tail -1 bridge/cs/Ui.cs` is `// my change`.
- **Replace.** With run B, run `OMASHARP_TEMPLATE_HIVE=$RUN_B/hive dotnet build -t:UpdateBridge -nologo | tee "$EVIDENCE/upd.txt"`. Each file the change touched and the app didn't prints `updated`, and `sha256sum` of each matches the checkout's `template/<file>`. `bridge/VERSION` is run B's.
- **Conflict.** Before the run B update, customize a file the change also touches. The update prints `CHANGED  <file>: you customized it; the new version is in <file>.upstream` with a diff, and the summary says `1 to merge by hand`. `<file>` keeps the customization, and `<file>.upstream` equals the checkout's file.
- **Still builds.** After the update, run `sh .agents/skills/omasharp/scripts/check.sh`. It passes, showing the change's behavior.

## Gotchas

- A run packs the checkout at `setup`. To get an "old" and a "new" template, make run A before the change (or from a `git stash`/`git worktree` of the previous commit) and run B after it.
- `UpdateBridge` compares against `bridge/MANIFEST` (what the app last received), not against git, so it works with uncommitted changes in the checkout. Bump `template/bridge/VERSION` anyway (CONTRIBUTING.md).
- `*.upstream` files are in `.gitignore`. Don't take their absence from `git status` to mean there was no conflict: read the update's output.
- `$EVIDENCE` is the folder `sh $S keep` printed, or any folder under `${TMPDIR:-/tmp}/omasharp-verify-evidence/`.
