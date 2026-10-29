#!/bin/sh
set -eu
src=$1; out=$2; exe=$3; id=$4; icons=$5
case $exe in *[\"\`\$\\]*) echo "desktop: can't write '$exe' into Exec" >&2; exit 1 ;; esac
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
  [ -z "$report" ] || printf '%s\n' "$report" | NEW=$new SRC=$src awk '{ if (index($0, ENVIRON["NEW"]) == 1) $0 = ENVIRON["SRC"] substr($0, length(ENVIRON["NEW"]) + 1); print }'
  [ "$status" -eq 0 ] || { echo "$src: error: not a valid desktop entry (above)" >&2; exit 1; }
fi
mkdir -p "$(dirname "$out")"
mv -f "$new" "$out"
