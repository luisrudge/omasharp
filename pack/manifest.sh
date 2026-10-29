#!/bin/sh
set -eu
root=$(cd "$(dirname "$0")/../template" && pwd)
list() {
  (cd "$root/$1" && find . -type f ! -name MANIFEST ! -path '*/bin/*' ! -path '*/obj/*' ! -name Makefile ! -name '*.o' ! -name 'moc_*' ! -name '*.moc' ! -name '*.pyc' \
    | sed 's|^\./||' | LC_ALL=C sort | while read -r f; do printf '%s  %s/%s\n' "$(sha256sum "$f" | cut -d' ' -f1)" "$2" "$f"; done)
}
{ list bridge bridge; list .agents/skills/omasharp .agents/skills/omasharp; } > "$1"
