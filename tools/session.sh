# shellcheck shell=sh
export XDG_RUNTIME_DIR="${XDG_RUNTIME_DIR:-/run/user/$(id -u)}"
MAIN_SIG=; MAIN_WL=
for s in $(hyprctl instances -j 2>/dev/null | jq -r 'sort_by(.time) | .[].instance'); do
  if HYPRLAND_INSTANCE_SIGNATURE=$s hyprctl monitors >/dev/null 2>&1; then
    MAIN_SIG=$s; MAIN_WL=$(hyprctl instances -j | jq -r --arg s "$s" '.[] | select(.instance == $s) | .wl_socket'); break
  fi
done
[ -n "$MAIN_SIG" ] || { echo "needs a running Hyprland session" >&2; exit 1; }
main() { HYPRLAND_INSTANCE_SIGNATURE=$MAIN_SIG WAYLAND_DISPLAY=$MAIN_WL hyprctl "$@"; }
