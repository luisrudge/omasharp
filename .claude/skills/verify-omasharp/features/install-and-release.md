# Install and release

A window app installs for the current user with `dotnet publish -t:Install` (into `~/.local/share/omasharp/<id>/`, plus `~/.local/bin/<id>` and a launcher entry) and uninstalls with `-t:Uninstall`. `-t:Package` makes a GitHub release (a tarball, `install.sh`, `SHA256SUMS`) that people install with `curl … | sh`, without .NET or sudo.

## Sub-features

- `inst-install`: `-t:Install` installs exactly `lib<id>.so`, `<id>`, `<id>.rcc`, the `~/.local/bin` link and `<id>.desktop`, using the app's own `app.desktop` when there is one.
- `inst-run`: the installed app starts and shows its text.
- `inst-uninstall`: `-t:Uninstall` leaves nothing in `~/.local`.
- `rel-package`: `-t:Package` writes `artifacts/` with `SHA256SUMS`, `install.sh` and `<id>-linux-x64.tar.gz`.
- `rel-install`: the release's `install.sh`, piped, installs, updates and uninstalls (`-s uninstall`), and refuses a download that doesn't match `SHA256SUMS`.
- `rel-panel`: panels have no release (`-t:Package` fails with a message).

## How to get to it (user POV)

- `dotnet publish -t:Install` and `dotnet build -t:Uninstall` in the app folder.
- Tag a version and push it; the release workflow runs `-t:Package`. Users then run `curl -fsSL https://github.com/<owner>/<repo>/releases/latest/download/install.sh | sh`.

## Driving it with the build targets in a stand-in HOME

Preconditions:

- A fresh window app: `APP=$(sh $S new "$RUN" Tally) && cd "$APP"`.
- Everything below runs with `HOME="$RUN/home"`; the real HOME is used only for dotnet's own files:
  `real=$HOME; cache=$(dotnet nuget locals global-packages --list | sed 's/^global-packages: //'); h=$RUN/home`
  `as_home() { HOME=$h DOTNET_CLI_HOME=$real NUGET_PACKAGES=$cache XDG_CACHE_HOME=${XDG_CACHE_HOME:-$real/.cache} "$@"; }`

- **Install.** Run `as_home dotnet publish -t:Install -v quiet -nologo`. `ls "$h/.local/share/omasharp/tally"` prints `libtally.so tally tally.rcc`. `readlink "$h/.local/bin/tally"` points there. `$h/.local/share/applications/tally.desktop` has `StartupWMClass=tally`.
- **Run installed.** Run `t=$RUN/tally.txt; env HOME=$h XDG_DATA_HOME=$h/.local/share XDG_CONFIG_HOME=$h/.config XDG_STATE_HOME=$h/.local/state QT_QPA_PLATFORM=offscreen QT_QUICK_BACKEND=software OMASHARP_TEXT_DUMP=$t timeout 5 "$h/.local/bin/tally"; cat "$t"`. It prints `Label: Nothing counted yet`.
- **Uninstall.** Run `as_home dotnet build -t:Uninstall -v quiet -nologo && find "$h/.local" ! -type d`. It prints nothing.
- **Package.** Run `as_home dotnet publish -t:Package -p:AppRepository=example/tally -v quiet -nologo && ls artifacts && (cd artifacts && sha256sum -c SHA256SUMS)`. It lists `SHA256SUMS install.sh tally-linux-x64.tar.gz`, and every checksum is `OK`.
- **Install the release.** Run `curl -fsSL "file://$APP/artifacts/install.sh" | env HOME=$h OMASHARP_RELEASE_URL="file://$APP/artifacts" sh`. It prints `Installed Tally 0.1.0`, and the files are as in Install.
- **Bad download.** Copy `artifacts` to `$RUN/bad`, append a byte to the tarball, and install from `file://$RUN/bad`. It exits non-zero with `doesn't match the release's SHA256SUMS; nothing was installed`, and the installed `tally` is unchanged (`sha256sum` before and after).
- **Uninstall the release.** Run `curl -fsSL "file://$APP/artifacts/install.sh" | env HOME=$h OMASHARP_RELEASE_URL="file://$APP/artifacts" sh -s uninstall && find "$h/.local" ! -type d`. It prints nothing.
- **Panel.** In a panel app, `dotnet publish -t:Package` fails with `Package makes window app releases`.

## Gotchas

- Never install into the real HOME while verifying: `-t:Install` adds a launcher entry to the user's menu.
- `HOME=$h DOTNET_CLI_HOME=$HOME …` on one line is unreliable in sh (assignment order). Save the real HOME first, as `as_home` does.
- `OMASHARP_RELEASE_URL` exists for this test only. Without it, `install.sh` downloads from github.com.
- Running the installed app in the stand-in HOME uses no Omarchy theme (there's none in `$h`). That's expected, not a bug.
