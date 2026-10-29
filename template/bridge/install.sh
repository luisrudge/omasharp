#!/bin/sh
set -eu
id='@APPID@' name='@NAME@' repo='@REPO@'
data=$HOME/.local/share
lib=$data/omasharp/$id
bin=$HOME/.local/bin/$id
entry=$data/applications/$id.desktop
icons=$data/icons/hicolor
die() { echo "$name: $*" >&2; exit 1; }

refresh() {
  apps=$data/applications
  if command -v update-desktop-database >/dev/null && { [ -f "$apps/mimeinfo.cache" ] || grep -qs '^MimeType=' "$apps"/*.desktop; }; then
    update-desktop-database -q "$apps" || true
    [ -f "$apps/mimeinfo.cache" ] && ! grep -qv '^\[MIME Cache\]$' "$apps/mimeinfo.cache" && rm -f "$apps/mimeinfo.cache"
  fi
  if command -v gtk-update-icon-cache >/dev/null && [ -f "$icons/icon-theme.cache" ]; then
    gtk-update-icon-cache -q -f -t "$icons" || true
  fi
}

lock() {
  mkdir -p "$data/omasharp"
  command -v flock >/dev/null || return 0
  exec 9>"$data/omasharp/.$id.lock"
  flock -n 9 || { echo "Waiting for another install of $name to finish..."; flock 9; }
}

case ${1:-} in
'') ;;
uninstall)
  lock
  rm -rf "$lib" "$entry" "$icons/scalable/apps/$id.svg" "$icons/scalable/apps/$id.png" "$data/omasharp/.$id.lock"
  [ -L "$bin" ] && rm -f "$bin"
  rmdir "$data/omasharp" 2>/dev/null || true
  refresh
  echo "Removed $name."
  exit 0
  ;;
*) die "usage: install.sh [uninstall]" ;;
esac

from=
[ "${0##*/}" != install.sh ] || [ ! -f "$0" ] || from=$(dirname -- "$0")
if [ -z "$from" ] || [ ! -f "$from/app/lib$id.so" ]; then
  [ -n "$repo" ] || die "this copy has no release to download; run the install.sh of an unpacked release"
  case $(uname -m) in
  x86_64) rid=linux-x64 ;;
  aarch64) rid=linux-arm64 ;;
  *) rid=linux-$(uname -m) ;;
  esac
  asset=$id-$rid.tar.gz
  base=https://github.com/$repo/releases/latest/download
  [ -z "${OMASHARP_RELEASE:-}" ] || base=https://github.com/$repo/releases/download/$OMASHARP_RELEASE
  base=${OMASHARP_RELEASE_URL:-$base}
  tmp=$(mktemp -d)
  trap 'rm -rf "$tmp"' EXIT
  echo "Downloading $base/$asset"
  curl -fsSL -o "$tmp/$asset" "$base/$asset" || die "no $asset in that release (is there a build for $rid?)"
  curl -fsSL -o "$tmp/SHA256SUMS" "$base/SHA256SUMS" || die "no SHA256SUMS in that release"
  (cd "$tmp" && grep "  $asset\$" SHA256SUMS | sha256sum -c --status) ||
    die "$asset doesn't match the release's SHA256SUMS; nothing was installed"
  tar -xzf "$tmp/$asset" -C "$tmp" --no-same-owner
  from=$tmp/$id
  [ -f "$from/app/lib$id.so" ] || die "$asset isn't a release of $id"
fi

missing=$(LC_ALL=C ldd "$from/app/$id" "$from/app/lib$id.so" 2>&1 | grep 'not found' | sed 's/^[[:space:]]*/  /' || true)
if [ -n "$missing" ]; then
  echo "$missing" >&2
  die "this system's libraries are missing or older than the ones it was built with (above). Update the system (Omarchy menu > Update) and run this again; nothing was installed."
fi

lock
mkdir -p "$data/applications" "$(dirname "$bin")"
new=$data/omasharp/.$id.new old=$data/omasharp/.$id.old
rm -rf "$new" "$old"
cp -R "$from/app" "$new"
chmod 755 "$new/$id"
[ ! -e "$lib" ] || mv "$lib" "$old"
mv "$new" "$lib"
rm -rf "$old"
ln -sfn "$lib/$id" "$bin"
for ext in svg png; do
  rm -f "$icons/scalable/apps/$id.$ext"
  [ ! -f "$from/$id.$ext" ] || { mkdir -p "$icons/scalable/apps" && cp "$from/$id.$ext" "$icons/scalable/apps/$id.$ext"; }
done
case $lib in *[\"\`\$\\]*) die "can't write '$lib' into a desktop entry" ;; esac
EXE=$lib/$id awk '{ s = $0; r = ""; while ((i = index(s, "@EXEC@")) > 0) { r = r substr(s, 1, i - 1) ENVIRON["EXE"]; s = substr(s, i + 6) } print r s }' \
  "$from/$id.desktop" >"$entry.new"
mv -f "$entry.new" "$entry"
refresh

version=$(cat "$from/version" 2>/dev/null || true)
echo "Installed $name${version:+ $version}: pick it in the app launcher, or run $id."
case :$PATH: in *":$HOME/.local/bin:"*) ;; *) echo "(~/.local/bin isn't on your PATH: the command is $bin)" ;; esac
