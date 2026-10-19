#!/bin/sh
# desktop.sh: the desktop entry part of `dotnet publish -t:Install` and `-t:Uninstall` (window apps).
#   desktop.sh SRC OUT EXEC APPID ICONS   write the app's own entry (app.desktop) to OUT: @EXEC@
#       becomes the installed binary, and [Desktop Entry] gets StartupWMClass=APPID, plus Icon=APPID
#       when ICONS isn't 0 (the app ships icon.svg or icon.png), replacing the ones already there.
#       desktop-file-validate checks it first, when it's installed: an invalid entry leaves OUT as it was.
#   desktop.sh --refresh DATA_DIR         refresh the launcher's caches in DATA_DIR (~/.local/share)
#       after an install or uninstall: the MIME cache, when the app's entries use one, and the icon
#       cache, only when there already is one. Neither is left behind when nothing needs it.
set -eu
if [ "$1" = --refresh ]; then
  apps=$2/applications; icons=$2/icons/hicolor
  if command -v update-desktop-database >/dev/null && { [ -f "$apps/mimeinfo.cache" ] || grep -qs '^MimeType=' "$apps"/*.desktop; }; then
    update-desktop-database -q "$apps" || true
    # No associations left (the app was the only one): an empty cache is the same as none.
    [ -f "$apps/mimeinfo.cache" ] && ! grep -qv '^\[MIME Cache\]$' "$apps/mimeinfo.cache" && rm -f "$apps/mimeinfo.cache"
  fi
  if command -v gtk-update-icon-cache >/dev/null && [ -f "$icons/icon-theme.cache" ]; then
    gtk-update-icon-cache -q -f -t "$icons" || true
  fi
  exit 0
fi
src=$1; out=$2; exe=$3; id=$4; icons=$5
case $exe in *[\"\`\$\\]*) echo "desktop: can't write '$exe' into Exec" >&2; exit 1 ;; esac
# Written and checked in a folder of its own, under OUT's name (the validator checks the name too).
tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT; new=$tmp/$(basename "$out")
EXE="\"$exe\"" ID=$id ICON=$([ "$icons" = 0 ] && echo 0 || echo 1) awk '
  function exec_path(s,   i, r) { r = ""; while ((i = index(s, "@EXEC@")) > 0) { r = r substr(s, 1, i - 1) ENVIRON["EXE"]; s = substr(s, i + 6) } return r s }
  /^\[/ { group = $0; print
          if (group == "[Desktop Entry]") { print "StartupWMClass=" ENVIRON["ID"]; if (ENVIRON["ICON"] == 1) print "Icon=" ENVIRON["ID"] }
          next }
  group == "[Desktop Entry]" && /^StartupWMClass[ \t]*=/ { next }
  group == "[Desktop Entry]" && ENVIRON["ICON"] == 1 && /^Icon[ \t]*=/ { next }
  { print exec_path($0) }' "$src" > "$new"
if command -v desktop-file-validate >/dev/null; then
  status=0; report=$(desktop-file-validate "$new" 2>&1) || status=$?
  # Its messages name the file it checked: point them at the app's own.
  [ -z "$report" ] || printf '%s\n' "$report" | NEW=$new SRC=$src awk '{ if (index($0, ENVIRON["NEW"]) == 1) $0 = ENVIRON["SRC"] substr($0, length(ENVIRON["NEW"]) + 1); print }'
  [ "$status" -eq 0 ] || { echo "$src: error: not a valid desktop entry (above), so it wasn't installed" >&2; exit 1; }
fi
mkdir -p "$(dirname "$out")"
mv -f "$new" "$out"
