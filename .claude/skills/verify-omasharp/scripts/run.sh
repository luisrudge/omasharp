#!/bin/sh
# run.sh: a private Omasharp, packed from this checkout, to verify changes the way users meet them
# (dotnet new omarchy-app, then the app's own check.sh). Like scripts/test.sh, it never touches your
# installed templates, NuGet sources, data or screen.
#   run.sh setup                 pack the analyzers and the template into a new run folder; prints its path
#   run.sh new RUN NAME [ARGS]   dotnet new omarchy-app -n NAME [ARGS] in RUN (e.g. --kind panel); prints the app's path
#   run.sh doctor RUN            is RUN worth driving: packed from the checkout as it is now, tools present
#   run.sh keep RUN APP LABEL    copy APP's last check (obj/shot.png, obj/shot.txt, logs) to the evidence folder
#   run.sh cleanup RUN           remove RUN and its analyzers package from the NuGet cache; evidence stays
# Evidence: ${TMPDIR:-/tmp}/omasharp-verify-evidence/<run>/<label>/
set -eu
root=$(cd "$(dirname "$0")/../../../.." && pwd)
evidence_root=${TMPDIR:-/tmp}/omasharp-verify-evidence
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
die() { echo "run.sh: $*" >&2; exit 1; }
# What a run packs: the template (bridge, skill), the analyzers and the package project.
sources() { (cd "$root" && find template analyzers/Omasharp.Analyzers pack -type f ! -path '*/bin/*' ! -path '*/obj/*' -print0 | sort -z | xargs -0 sha256sum | sha256sum | cut -d' ' -f1); }
run_dir() {
  [ -n "${1:-}" ] || die "which run? (the folder 'run.sh setup' printed)"
  [ -f "$1/analyzers-version" ] || die "$1 isn't a run folder made by 'run.sh setup'"
  (cd "$1" && pwd)
}

case ${1:-} in
setup)
  run=$(mktemp -d "${TMPDIR:-/tmp}/omasharp-verify.XXXXXX")
  # A version of the analyzers package for this run only (lowercase: NuGet's cache folders are).
  v=0.0.0-verify.$(basename "$run" | sed 's/^omasharp-verify\.//' | tr '[:upper:]' '[:lower:]')
  sources > "$run/sources"
  dotnet pack "$root/analyzers/Omasharp.Analyzers" -o "$run/pkg" -p:Version="$v" -v quiet -nologo > "$run/setup.log" 2>&1 || { tail -20 "$run/setup.log"; die "packing the analyzers failed"; }
  dotnet pack "$root/pack" -o "$run/pkg" -v quiet -nologo >> "$run/setup.log" 2>&1 || { tail -20 "$run/setup.log"; die "packing the template failed"; }
  dotnet new install "$run"/pkg/Omasharp.Templates.*.nupkg --debug:custom-hive "$run/hive" >> "$run/setup.log" 2>&1 || { tail -20 "$run/setup.log"; die "installing the template into the run's hive failed"; }
  # Apps made in the run find this package (NuGet.config) at this version (Directory.Build.props).
  printf '<configuration><packageSources><add key="omasharp-verify" value="%s" /></packageSources></configuration>\n' "$run/pkg" > "$run/NuGet.config"
  printf '<Project><PropertyGroup><OmasharpAnalyzersVersion>%s</OmasharpAnalyzersVersion></PropertyGroup></Project>\n' "$v" > "$run/Directory.Build.props"
  mkdir -p "$run/data" "$run/home"
  echo "$v" > "$run/analyzers-version"
  echo "$run"
  ;;
new)
  run=$(run_dir "${2:-}"); name=${3:-}; [ -n "$name" ] || die "new RUN NAME [ARGS]"; shift 3
  [ ! -e "$run/$name" ] || die "$run/$name already exists"
  (cd "$run" && dotnet new omarchy-app --debug:custom-hive "$run/hive" -n "$name" "$@" > /dev/null) || die "dotnet new omarchy-app -n $name $* failed"
  echo "$run/$name"
  ;;
doctor)
  run=$(run_dir "${2:-}"); bad=0
  ok() { echo "ok    $*"; }
  no() { echo "FAIL  $*"; bad=1; }
  version=$(cat "$root/template/bridge/VERSION")
  if [ -f "$run/pkg/Omasharp.Templates.$version.nupkg" ]; then ok "template $version packed from $root"; else no "no Omasharp.Templates.$version.nupkg in $run/pkg"; fi
  if [ "$(sources)" = "$(cat "$run/sources")" ]; then ok "template, analyzers and pack unchanged since setup"
  else no "template/, analyzers/ or pack/ changed since setup: 'run.sh cleanup $run', then 'run.sh setup'"; fi
  if dotnet new list omarchy-app --debug:custom-hive "$run/hive" > /dev/null 2>&1; then ok "omarchy-app installed in $run/hive"; else no "omarchy-app isn't in $run/hive"; fi
  for t in dotnet qmake6 g++ clang; do
    if command -v "$t" > /dev/null; then ok "$t"; else no "$t not found (see CONTRIBUTING.md, Setup)"; fi
  done
  if hyprctl instances -j 2> /dev/null | jq -e 'length > 0' > /dev/null 2>&1; then ok "Hyprland: check.sh can use its private compositor (screenshots, --keys, panels)"
  else echo "note  no Hyprland session: window apps with check.sh --offscreen only (visible text, no screenshot or keys, no panels)"; fi
  for t in grim wtype jq dbus-run-session quickshell; do command -v "$t" > /dev/null || echo "note  $t not found: check.sh's private compositor needs it"; done
  exit $bad
  ;;
keep)
  run=$(run_dir "${2:-}"); app=${3:-}; label=${4:-}
  [ -n "$app" ] && [ -n "$label" ] || die "keep RUN APP LABEL"
  [ -d "$app/obj" ] || die "$app has no obj/: run its check.sh first"
  dest=$evidence_root/$(basename "$run")/$label; mkdir -p "$dest"
  for f in obj/shot.png obj/shot.txt obj/omasharp/run.log obj/omasharp/build.log; do
    [ ! -f "$app/$f" ] || cp "$app/$f" "$dest/"
  done
  ls "$dest"/* > /dev/null 2>&1 || die "nothing to keep in $app/obj"
  echo "$dest"
  ;;
cleanup)
  run=$(run_dir "${2:-}"); v=$(cat "$run/analyzers-version")
  cache=$(dotnet nuget locals global-packages --list | sed 's/^global-packages: //')
  rm -rf "$run" "${cache%/}/omasharp.analyzers/$v"
  rmdir "${cache%/}/omasharp.analyzers" 2> /dev/null || true
  echo "removed $run (evidence, if any: $evidence_root/$(basename "$run")/)"
  ;;
*) sed -n '2,10p' "$0" | sed 's/^# \{0,1\}//'; [ "${1:-}" = -h ] || [ "${1:-}" = --help ] ;;
esac
