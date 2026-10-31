#!/bin/sh
set -u
out=$1; log=$2; shift 2
here=$(cd "$(dirname "$0")" && pwd)
sq() { printf "'%s'" "$(printf '%s' "$1" | sed "s/'/'\\\\''/g")"; }
q() { printf ' '; sq "$1"; }
size=${OMASHARP_SHOT_SIZE:-}; [ -n "$size" ] || size=1280x960
{ printf '%s' "$size" | grep -Eqx '[1-9][0-9]*x[1-9][0-9]*' && [ "${size%x*}" -ge 2 ] && [ "${size#*x}" -ge 2 ]; } ||
  { echo "shot: bad size '$size' (use WxH, at least 2x2, like 1280x960)" >&2; exit 1; }
keys=; [ -n "${OMASHARP_SHOT_KEYS_FILE:-}" ] && [ -s "$OMASHARP_SHOT_KEYS_FILE" ] && keys=$(cat "$OMASHARP_SHOT_KEYS_FILE")
export XDG_RUNTIME_DIR="${XDG_RUNTIME_DIR:-/run/user/$(id -u)}"
main_sig=; main_wl=
for s in $(hyprctl instances -j 2>/dev/null | jq -r 'sort_by(.time) | .[].instance'); do
  if HYPRLAND_INSTANCE_SIGNATURE=$s hyprctl monitors >/dev/null 2>&1; then
    main_sig=$s; main_wl=$(hyprctl instances -j | jq -r --arg s "$s" '.[] | select(.instance == $s) | .wl_socket'); break
  fi
done
[ -n "$main_sig" ] || { echo "shot: needs a running Hyprland session" >&2; exit 1; }
main() { HYPRLAND_INSTANCE_SIGNATURE=$main_sig WAYLAND_DISPLAY=$main_wl hyprctl "$@"; }
tmp=$(mktemp -d /tmp/omasharp-shot.XXXXXX); nest_pid=; nest_sig=; app_pid=
# shellcheck disable=SC2013,SC2329 # child pids are single words; cleanup runs from the trap below
tree() { echo "$1"; for c in $(cat "/proc/$1/task/"*/children 2>/dev/null); do tree "$c"; done; }
# shellcheck disable=SC2329
cleanup() {
  procs=; [ -n "$app_pid" ] && procs=$(tree "$app_pid")
  for p in $procs; do kill "$p" 2>/dev/null; done; sleep 0.3; for p in $procs; do kill -9 "$p" 2>/dev/null; done
  [ -n "$nest_pid" ] && { kill "$nest_pid" 2>/dev/null; sleep 0.3; kill -9 "$nest_pid" 2>/dev/null; }
  [ -n "$nest_sig" ] && [ "$nest_sig" != "$main_sig" ] && rm -rf "$XDG_RUNTIME_DIR/hypr/$nest_sig"
  rm -rf "$tmp"
}
trap cleanup EXIT INT TERM

ws="special:omasharp-shot"
{ echo '#!/bin/sh'; echo "echo \$\$ >$(q "$tmp/nest.pid")"
  echo "export HYPRLAND_NO_SD_VARS=1 HYPRLAND_NO_SD_NOTIFY=1 HYPRLAND_NO_CRASHREPORTER=1 WAYLAND_DISPLAY=$main_wl"
  echo 'unset HYPRLAND_INSTANCE_SIGNATURE DISPLAY'
  echo "exec Hyprland --config$(q "$here/shot-hyprland.lua") >$(q "$tmp/hyprland.log") 2>&1"; } > "$tmp/nest"
chmod +x "$tmp/nest"
main dispatch "hl.dsp.exec_cmd(\"$tmp/nest\", { workspace = \"$ws silent\" })" >/dev/null
for _ in $(seq 100); do
  nest_pid=$(cat "$tmp/nest.pid" 2>/dev/null)
  [ -n "$nest_pid" ] && nest_sig=$(hyprctl instances -j | jq -r --argjson p "$nest_pid" '.[] | select(.pid == $p) | .instance')
  [ -n "$nest_sig" ] && break; sleep 0.1
done
[ -n "$nest_sig" ] || { echo "shot: nested Hyprland didn't start" >&2; tail -5 "$tmp/hyprland.log" >&2; exit 1; }
export HYPRLAND_INSTANCE_SIGNATURE="$nest_sig"
nest_wl=$(hyprctl instances -j | jq -r --arg s "$nest_sig" '.[] | select(.instance == $s) | .wl_socket')
export WAYLAND_DISPLAY="$nest_wl"
sleep 0.5

hyprctl output create headless shot >/dev/null
w=${size%x*}; h=${size#*x}; w=$((w / 2 * 2)); h=$((h / 2 * 2))
[ "${w}x$h" = "$size" ] || echo "shot: using ${w}x$h, not $size: 1.5x scale needs even sizes"
hyprctl eval "hl.monitor({ output = \"shot\", mode = \"$((w * 3 / 2))x$((h * 3 / 2))\", position = \"auto\", scale = 1.5 })" >/dev/null
sleep 0.3
got=$(hyprctl monitors -j | jq -r '.[] | select(.name=="shot") | "\(.width)x\(.height)@\(.scale)"')
[ "$got" = "$((w * 3 / 2))x$((h * 3 / 2))@1.5" ] || { echo "shot: the screen is $got, not $((w * 3 / 2))x$((h * 3 / 2)) at 1.5x" >&2; exit 1; }
hyprctl dispatch 'hl.dsp.focus({ monitor = "shot" })' >/dev/null
nws=$(hyprctl monitors -j | jq -r '.[] | select(.name=="shot") | .activeWorkspace.id')
[ -n "${OMASHARP_SHOT_FLOAT:-}" ] && hyprctl eval 'hl.window_rule({ match = { class = ".*" }, float = true, center = true })' >/dev/null

{ echo '#!/bin/sh'; echo "echo \$\$ > '$tmp/app.pid'"
  if [ -n "${OMASHARP_SHOT_ENV_FILE:-}" ] && [ -s "$OMASHARP_SHOT_ENV_FILE" ]; then
    while IFS= read -r e; do
      printf '%s' "${e%%=*}" | grep -Eqx '[A-Za-z_][A-Za-z0-9_]*' || { echo "shot: bad environment variable '$e' (use KEY=VALUE)" >&2; exit 1; }
      printf '%s\n' "export ${e%%=*}=$(sq "${e#*=}")"
    done < "$OMASHARP_SHOT_ENV_FILE"
  fi
  echo "export OMASHARP_PANEL_PAYLOAD='{\"screen\":\"shot\"}' QT_FORCE_STDERR_LOGGING=1 OMASHARP_TEXT_DUMP=$(sq "$tmp/text.txt")"
  echo "export WAYLAND_DISPLAY=$(sq "$nest_wl") XDG_RUNTIME_DIR=$(sq "$XDG_RUNTIME_DIR"); unset DISPLAY WAYLAND_SOCKET QT_QPA_PLATFORM"
  echo "export OMASHARP_PLATFORMTHEME=generic QT_QPA_PLATFORMTHEME=generic NO_AT_BRIDGE=1 BROWSER=true"
  extra=; [ -n "${OMASHARP_SHOT_ARGS_FILE:-}" ] && [ -s "$OMASHARP_SHOT_ARGS_FILE" ] && extra=$(cat "$OMASHARP_SHOT_ARGS_FILE")
  printf 'exec dbus-run-session --config-file=%s --' "$(sq "$here/shot-dbus.conf")"; for a in "$@"; do q "$a"; done
  set -f; for a in $extra; do q "$a"; done; set +f
  echo " >$(q "$log") 2>&1"; } > "$tmp/app"
chmod +x "$tmp/app"; : > "$tmp/text.txt"
hyprctl dispatch "hl.dsp.exec_cmd(\"$tmp/app\", { workspace = \"$nws silent\" })" >/dev/null
for _ in $(seq 100); do app_pid=$(cat "$tmp/app.pid" 2>/dev/null); [ -n "$app_pid" ] && break; sleep 0.05; done
sleep "${OMASHARP_SHOT_WAIT:-3}"
if [ -n "$keys" ]; then
  addr=$(hyprctl clients -j | jq -r ".[] | select(.workspace.id==$nws) | .address" | head -1)
  [ -n "$addr" ] && hyprctl dispatch "hl.dsp.focus({ window = \"address:$addr\" })" >/dev/null
  sleep 0.2; python3 "$here/keys.py" "$keys"; sleep 0.8
fi
sleep 0.3
size_now=$(hyprctl clients -j | jq -r ".[] | select(.workspace.id==$nws) | \"\\(.size[0])x\\(.size[1])\"" | head -1)
if [ -n "$size_now" ] && { [ "${size_now%x*}" -gt "$w" ] || [ "${size_now#*x}" -gt "$h" ]; }; then
  echo "shot: window is $size_now, larger than the ${w}x$h screen: use --size" >&2
fi
fail=0
if grim -o shot "$out"; then echo "Screenshot: $out (open it and look)"; else echo "shot: screenshot failed" >&2; fail=1; fi
txt=${out%.png}.txt
if [ -s "$tmp/text.txt" ]; then cp "$tmp/text.txt" "$txt"; echo "--- visible text, top to bottom ($txt) ---"; cat "$txt"; fi
if [ -z "$app_pid" ] || ! kill -0 "$app_pid" 2>/dev/null; then echo "shot: the app didn't start or exited early (see its output below)" >&2; fail=1; fi
output=$(grep -vE "register with host portal|org.freedesktop.DBus.Error.ServiceUnknown|INFO.*(Launching config|Shell ID|Saving logs|Configuration Loaded)" "$log" 2>/dev/null | tail -40)
[ -n "$output" ] && { echo "--- app output ($log) ---"; echo "$output"; }
exit $fail
