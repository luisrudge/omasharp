#!/bin/sh
# scripts/test.sh: the whole check, as CI runs it. Needs: dotnet SDK 10+, qmake6/g++ (qt6-base,
# qt6-declarative), clang (native AOT link). Uses a private template store and temp folders:
# it never touches your installed templates, your screen or your files.
#   1. the analyzers' unit tests, then pack them and the template (generated apps restore
#   Omasharp.Analyzers from that local package); 2. a new window app through the skill's check.sh
#   (unit tests, dev and native AOT builds, each run headless on Qt's offscreen platform, checking
#   the visible text; --size, --env, empty XDG dirs, DrawView's shapes, lines and areas, the theme
#   name); a new panel app: build, tests, AOT publish, its stdio protocol; 3. every omasharp rule
#   fails a generated app's build; 4. bridge update on a fresh app is a no-op; 5. SQLite the way the skill
#   says (window app installed and run twice, panel), then -t:Install with the app's own app.desktop
#   and -t:Uninstall; a release (-t:Package) installed, updated and removed by its install.sh as
#   users run it; 6. the examples, through check.sh. Warnings fail every build (Omasharp.targets).
set -eu
root=$(cd "$(dirname "$0")/.." && pwd)
work=$(mktemp -d "${TMPDIR:-/tmp}/omasharp-test.XXXXXX")
# A version of the analyzers package for this run only, removed from the NuGet cache afterwards.
export OmasharpAnalyzersVersion="0.0.0-test.$$"
nuget_cache=$(dotnet nuget locals global-packages --list | sed 's/^global-packages: //')
trap 'rm -rf "$work" "${nuget_cache%/}/omasharp.analyzers/$OmasharpAnalyzersVersion"; rmdir "${nuget_cache%/}/omasharp.analyzers" 2>/dev/null || true' EXIT
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
export OMASHARP_TEMPLATE_HIVE="$work/hive"
# Panel builds construct the backend (--qml-shim), and apps keep their data here: never in yours.
export XDG_DATA_HOME="$work/data"
step() { printf '\n== %s\n' "$*"; }
fail() { echo "FAIL: $*" >&2; exit 1; }

step "analyzers: unit tests"
dotnet run --project "$root/analyzers/Omasharp.Analyzers.Tests" >"$work/analyzers.log" 2>&1 || { cat "$work/analyzers.log"; fail "analyzer tests"; }
tail -1 "$work/analyzers.log"

step "pack the analyzers and the template"
dotnet pack "$root/analyzers/Omasharp.Analyzers" -o "$work/pkg" -p:Version="$OmasharpAnalyzersVersion" -v quiet -nologo >/dev/null
# Generated apps (under $work) find Omasharp.Analyzers here; nuget.org stays for everything else.
printf '<configuration><packageSources><add key="omasharp-test" value="%s" /></packageSources></configuration>\n' "$work/pkg" > "$work/NuGet.config"
dotnet pack "$root/pack" -o "$work/pkg" -v quiet -nologo >/dev/null
dotnet new install "$work"/pkg/Omasharp.Templates.*.nupkg --debug:custom-hive "$OMASHARP_TEMPLATE_HIVE" >/dev/null
new() { (cd "$work" && dotnet new omarchy-app --debug:custom-hive "$OMASHARP_TEMPLATE_HIVE" "$@" >/dev/null); }

check="sh $root/template/.agents/skills/omasharp/scripts/check.sh"

step "window app: the files agents need"
new -n Win
for f in AGENTS.md CLAUDE.md .editorconfig .agents/skills/omasharp/SKILL.md .agents/skills/omasharp/scripts/check.sh bridge/MANIFEST .github/workflows/release.yml; do
  [ -f "$work/Win/$f" ] || fail "window app: no $f"
done
grep -q "  .agents/skills/omasharp/SKILL.md$" "$work/Win/bridge/MANIFEST" || fail "MANIFEST doesn't cover the skill"
grep -qx "@AGENTS.md" "$work/Win/CLAUDE.md" || fail "CLAUDE.md doesn't import AGENTS.md"
cmp -s "$root/template/.github/workflows/release.yml" "$work/Win/.github/workflows/release.yml" || fail "the release workflow changed on the way into the app"
# The release workflow's tag check reads the version this way.
[ "$(cd "$work/Win" && dotnet msbuild -getProperty:Version)" = 0.1.0 ] || fail "window app: no <Version> 0.1.0"

step "--app-id sets the app id"
new -n Named --app-id named-app
grep -q "<AppId>named-app</AppId>" "$work/Named/Named.csproj" || fail "--app-id: AppId not set"

step "window app: check.sh (tests, dev build, offscreen run), then --release"
(cd "$work/Win" && sh .agents/skills/omasharp/scripts/check.sh --offscreen && sh .agents/skills/omasharp/scripts/check.sh --offscreen --release --no-tests) >"$work/win.log" 2>&1 || { tail -30 "$work/win.log"; fail "window app"; }
grep -A3 "visible text" "$work/win.log"
[ "$(grep -c "Nothing counted yet" "$work/win.log")" -eq 2 ] || fail "window app: expected text not shown in both builds"

step "check.sh --size with odd numbers keeps the 1.5x scale (private compositor; needs Hyprland)"
if hyprctl instances -j 2>/dev/null | jq -e 'length > 0' >/dev/null 2>&1; then
  (cd "$work/Win" && sh .agents/skills/omasharp/scripts/check.sh --no-tests --size 625x751) >"$work/win-size.log" 2>&1 || { tail -20 "$work/win-size.log"; fail "check.sh --size 625x751"; }
  grep -q "using 624x750, not 625x751" "$work/win-size.log" || { cat "$work/win-size.log"; fail "check.sh --size: no note about the rounded size"; }
  # 624x750 logical pixels at 1.5x: a 936x1125 screenshot (the PNG's IHDR).
  px=$(python3 -I -c 'import struct, sys; print("%dx%d" % struct.unpack(">II", open(sys.argv[1], "rb").read(24)[16:24]))' "$work/Win/obj/shot.png")
  echo "screenshot: $px"
  [ "$px" = 936x1125 ] || fail "check.sh --size 625x751: the screenshot is $px, not 936x1125 (1.5x)"
  # Single digits are sizes too.
  (cd "$work/Win" && sh .agents/skills/omasharp/scripts/check.sh --no-tests --size 8x6) >"$work/win-size-small.log" 2>&1 || { tail -20 "$work/win-size-small.log"; fail "check.sh --size 8x6"; }
else
  echo "skipped: no Hyprland session"
fi
# Sizes it can't use fail before anything starts: 1 (0 at 1.5x), 0, and a leading zero (octal to sh).
for bad in 1x8 0x8 08x8; do
  OMASHARP_SHOT_SIZE=$bad sh "$root/template/.agents/skills/omasharp/scripts/shot.sh" "$work/bad.png" "$work/bad.log" true 2>"$work/bad-size.err" && fail "shot.sh: size $bad was accepted"
  grep -q "bad size '$bad'" "$work/bad-size.err" || { cat "$work/bad-size.err"; fail "shot.sh: no message for size $bad"; }
done

step "check.sh --env reaches the app as it is (--offscreen)"
cp "$work/Win/Backend.cs" "$work/Backend.cs.good"; cp "$work/Win/Main.qml" "$work/Main.qml.good"
sed -i '/^public class Backend : BackendBase$/{n;a\
    public string Env => Environment.GetEnvironmentVariable("CHECK_ENV") ?? "";
}' "$work/Win/Backend.cs"
sed -i '0,/id: win/s//id: win\n  Label { text: "env: " + backend.env }/' "$work/Win/Main.qml"
# shellcheck disable=SC2016 # literally: the app must see $HOME and backticks unexpanded
value='a '\''b'\'' "c" $HOME `id` \x; d=e'
(cd "$work/Win" && sh .agents/skills/omasharp/scripts/check.sh --offscreen --no-tests --env "CHECK_ENV=$value" --env QT_QUICK_BACKEND=software) >"$work/win-env.log" 2>&1 || { tail -20 "$work/win-env.log"; fail "check.sh --env"; }
grep -qF "Label: env: $value" "$work/Win/obj/shot.txt" || { grep "env:" "$work/win-env.log"; fail "check.sh --env: the value didn't reach the app unchanged"; }
grep -F "env:" "$work/Win/obj/shot.txt"
(cd "$work/Win" && sh .agents/skills/omasharp/scripts/check.sh --offscreen --no-tests --env QT_QPA_PLATFORM=wayland) >"$work/win-env-reserved.log" 2>&1 && fail "check.sh --env: QT_QPA_PLATFORM was accepted"
grep -q "can't set QT_QPA_PLATFORM" "$work/win-env-reserved.log" || { cat "$work/win-env-reserved.log"; fail "check.sh --env: no message for a reserved variable"; }
cp "$work/Backend.cs.good" "$work/Win/Backend.cs"; cp "$work/Main.qml.good" "$work/Win/Main.qml"

step "check.sh gives the app empty XDG dirs, not the real ones; --seed, --real-data (--offscreen)"
sed -i '/^public class Backend : BackendBase$/{n;a\
    public Backend() { var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify), "win"); Directory.CreateDirectory(d); File.WriteAllText(Path.Combine(d, "written.txt"), "x"); var s = Path.Combine(d, "seed.txt"); Seed = File.Exists(s) ? File.ReadAllText(s).Trim() : "none"; }\
    public string Seed { get; }
}' "$work/Win/Backend.cs"
sed -i '0,/id: win/s//id: win\n  Label { text: "seed: " + backend.seed }/' "$work/Win/Main.qml"
# A stand-in for the user's real data folder: a check must leave it empty.
real=$work/real-data; mkdir -p "$real" "$work/seed/data/win"; echo planted > "$work/seed/data/win/seed.txt"
xcheck() { (cd "$work/Win" && XDG_DATA_HOME=$real sh .agents/skills/omasharp/scripts/check.sh --offscreen --no-tests "$@"); }
xcheck >"$work/win-xdg.log" 2>&1 || { tail -20 "$work/win-xdg.log"; fail "check.sh: app writing to XDG_DATA_HOME"; }
[ -z "$(ls -A "$real")" ] || fail "check.sh: the app wrote to the real XDG_DATA_HOME: $(ls -A "$real")"
[ -f "$work/Win/obj/omasharp/xdg/data/win/written.txt" ] || fail "check.sh: the app's file isn't in obj/omasharp/xdg/data"
grep -q "seed: none" "$work/Win/obj/shot.txt" || { cat "$work/Win/obj/shot.txt"; fail "check.sh: the app didn't start from empty data"; }
xcheck --seed "$work/seed" >"$work/win-seed.log" 2>&1 || { tail -20 "$work/win-seed.log"; fail "check.sh --seed"; }
grep -q "seed: planted" "$work/Win/obj/shot.txt" || { cat "$work/Win/obj/shot.txt"; fail "check.sh --seed: the seeded file didn't reach the app"; }
[ ! -e "$work/seed/data/win/written.txt" ] && [ -z "$(ls -A "$real")" ] || fail "check.sh --seed: the app wrote to the seed or the real folder"
xcheck --real-data >"$work/win-real.log" 2>&1 || { tail -20 "$work/win-real.log"; fail "check.sh --real-data"; }
[ -f "$real/win/written.txt" ] || fail "check.sh --real-data: the app didn't get the real XDG_DATA_HOME"
xcheck --env XDG_DATA_HOME=/tmp >"$work/win-xdg-env.log" 2>&1 && fail "check.sh --env: XDG_DATA_HOME was accepted"
grep -q "can't set XDG_DATA_HOME" "$work/win-xdg-env.log" || { cat "$work/win-xdg-env.log"; fail "check.sh --env: no message for XDG_DATA_HOME"; }
# Data it can't remove (a read-only folder) fails the check instead of starting from it.
if [ "$(id -u)" != 0 ]; then
  locked=$work/Win/obj/omasharp/xdg/data/locked; mkdir -p "$locked"; touch "$locked/left"; chmod 555 "$locked"
  xcheck >"$work/win-xdg-locked.log" 2>&1 && { chmod 755 "$locked"; fail "check.sh: ran on data it couldn't remove"; }
  chmod 755 "$locked"
  grep -q "couldn't empty" "$work/win-xdg-locked.log" || { cat "$work/win-xdg-locked.log"; fail "check.sh: no message for data it couldn't remove"; }
fi
echo "the app's files went to obj/omasharp/xdg/, --seed and --real-data work"
cp "$work/Backend.cs.good" "$work/Win/Backend.cs"; cp "$work/Main.qml.good" "$work/Win/Main.qml"

step "DrawView draws its shapes under Qt's software renderer (--offscreen)"
sed -i '/^public class Backend : BackendBase$/{n;a\
    public string Snapshot => Environment.GetEnvironmentVariable("SNAPSHOT") ?? "";\
    public void Draw() { var d = new Drawing(1, 0, 0); d.Rect(0, 0, 200, 100, Color.Parse("#ff0000")); d.Rect(40000, 40000, 1, 1, Color.Parse("#00ff00")); Bridge.Show("box", d); }\
    public void DrawLine() { var d = new Drawing(1, 0, 0); d.Rect(0, 0, 1, 1, Color.Parse("#ff0000")); d.Rect(2e8, 0, 1, 1, Color.Parse("#ff0000")); Bridge.Show("line", d); }\
    public long PeakMb() => long.Parse(File.ReadLines("/proc/self/status").First(l => l.StartsWith("VmHWM:", StringComparison.Ordinal)).Split(" ", StringSplitOptions.RemoveEmptyEntries)[1]) / 1024;
}' "$work/Win/Backend.cs"
cat > "$work/Win/Main.qml" <<'EOF'
import QtQuick
import Omasharp

Window {
  visible: true
  width: 400
  height: 300
  Text { text: "drawn" }
  DrawView { id: box; width: 200; height: 100; source: "box"; view: [1, 0, 0] }
  DrawView { y: 120; width: 200; height: 10; source: "line"; view: [1, 0, 0] }
  Text { id: peak; y: 140 }
  Component.onCompleted: { backend.draw(); backend.drawLine() }
  Timer { interval: 300; running: true; onTriggered: box.grabToImage(r => { r.saveToFile(backend.snapshot); peak.text = "peak MB: " + backend.peakMb() }) }
}
EOF
(cd "$work/Win" && sh .agents/skills/omasharp/scripts/check.sh --offscreen --no-tests --env "SNAPSHOT=$work/box.ppm") >"$work/win-draw.log" 2>&1 || { tail -20 "$work/win-draw.log"; fail "DrawView app"; }
# A second shape far away: the image stays bounded (40000 x 40000 pixels would be 6 GB) and the box still shows.
# The share of red pixels in the grabbed DrawView (binary PPM: four header fields, then RGB bytes).
red=$(python3 -I -c 'import sys; d = open(sys.argv[1], "rb").read(); f = d.split(maxsplit=4); n = int(f[1]) * int(f[2]); px = d[-n * 3:]
print(sum(px[i:i + 3] == b"\xff\x00\x00" for i in range(0, n * 3, 3)) * 100 // n)' "$work/box.ppm" 2>/dev/null || echo 0)
echo "red: $red% of the DrawView"
[ "$red" -ge 90 ] || fail "DrawView: the software renderer drew no shapes"
# A long, thin drawing (2e8 x 1, a 230 MB image without a cap on each side) stays bounded too:
# the app's peak memory after drawing both.
peak=$(sed -n 's/.*peak MB: \([0-9]*\).*/\1/p' "$work/Win/obj/shot.txt")
echo "peak memory: ${peak:-?} MB"
[ -n "$peak" ] && [ "$peak" -lt 300 ] || fail "DrawView: the software renderer's image wasn't bounded (peak ${peak:-?} MB)"
cp "$work/Backend.cs.good" "$work/Win/Backend.cs"; cp "$work/Main.qml.good" "$work/Win/Main.qml"

step "Drawing: Line, Polyline and Area, encoded (unit test) and drawn (--offscreen)"
cp "$work/Win/tests/Tests.cs" "$work/Tests.cs.good"
cat > "$work/Win/tests/Tests.cs" <<'EOF'
global using AppBackend = Backend;
using System.Buffers.Binary;
using Omasharp;

// The buffer Drawing writes for lines and areas, as host/drawview.cpp reads it.
var d = new Drawing(2, 10, 20);
d.Line(0, 1, 2, 3, 1.5, Color.Parse("#ff0000"));
d.Polyline([(0, 0), (10, 5), (20, 0)], 2, Color.Parse("#8000ff00"));
d.Area([(0, 10), (10, 0)], 10, Color.Parse("#0000ff"));
d.Polyline([(0, 0)], 2, Color.Parse("#ff0000"));        // one point: nothing
d.Polyline([(0, 0), (1, 1)], 0, Color.Parse("#ff0000")); // no width: nothing
d.Area([(0, 0), (1, 1)], 5, Color.Parse("#00ff0000"));   // transparent: nothing
var expected = new List<byte>("ODL1"u8.ToArray());
void D(double v) { var b = new byte[8]; BinaryPrimitives.WriteDoubleLittleEndian(b, v); expected.AddRange(b); }
void I(int v) { var b = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(b, v); expected.AddRange(b); }
void U(uint v) { var b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, v); expected.AddRange(b); }
void F(float v) { var b = new byte[4]; BinaryPrimitives.WriteSingleLittleEndian(b, v); expected.AddRange(b); }
D(2); D(10); D(20); I(3);
expected.Add(5); F(1.5f); U(0xFFFF0000); I(2); F(0); F(1); F(2); F(3);
expected.Add(5); F(2); U(0x8000FF00); I(3); F(0); F(0); F(10); F(5); F(20); F(0);
expected.Add(6); F(10); U(0xFF0000FF); I(2); F(0); F(10); F(10); F(0);
var ok = d.Count == 3 && d.ToBytes().AsSpan().SequenceEqual(expected.ToArray());
Console.WriteLine(ok ? "ok   Line, Polyline and Area encode as host/drawview.cpp reads them" : $"FAIL Drawing encoding: {Convert.ToHexString(d.ToBytes())}");
return ok ? 0 : 1;
EOF
(cd "$work/Win" && dotnet run --project tests) >"$work/drawing-tests.log" 2>&1 || { tail -20 "$work/drawing-tests.log"; fail "Drawing: encoding"; }
grep -E "^(ok|FAIL)" "$work/drawing-tests.log"
cp "$work/Tests.cs.good" "$work/Win/tests/Tests.cs"
# A line over an area (a peak the corner must fill), a steep spike whose corner must be cut, and
# the same spike half transparent on white (drawn once everywhere: no darker overlap inside it).
sed -i '/^public class Backend : BackendBase$/{n;a\
    public string Snapshot => Environment.GetEnvironmentVariable("SNAPSHOT") ?? "";\
    public void Draw() { var d = new Drawing(); (double X, double Y)[] p = [(0, 80), (70, 20), (140, 80)]; d.Area(p, 100, Color.Parse("#0000ff")); d.Polyline(p, 4, Color.Parse("#ff0000")); d.Polyline([(160, 100), (165, 30), (170, 100)], 4, Color.Parse("#ff0000")); d.Rect(175, 0, 25, 100, Color.Parse("#ffffff")); d.Polyline([(180, 100), (185, 30), (190, 100)], 4, Color.Parse("#80ff0000")); Bridge.Show("chart", d); }
}' "$work/Win/Backend.cs"
cat > "$work/Win/Main.qml" <<'EOF'
import QtQuick
import Omasharp

Window {
  visible: true
  width: 400
  height: 300
  Text { y: 120; text: "drawn" }
  DrawView { id: chart; width: 200; height: 100; source: "chart"; view: [1, 0, 0] }
  Component.onCompleted: backend.draw()
  Timer { interval: 300; running: true; onTriggered: chart.grabToImage(r => r.saveToFile(backend.snapshot)) }
}
EOF
(cd "$work/Win" && sh .agents/skills/omasharp/scripts/check.sh --offscreen --no-tests --env "SNAPSHOT=$work/chart.ppm") >"$work/win-chart.log" 2>&1 || { tail -20 "$work/win-chart.log"; fail "DrawView chart app"; }
# Pixels of the grabbed DrawView (binary PPM), in the drawing's coordinates.
python3 -I - "$work/chart.ppm" <<'EOF' || fail "DrawView: lines and areas aren't drawn as expected"
import sys
d = open(sys.argv[1], "rb").read(); f = d.split(maxsplit=4); w, h = int(f[1]), int(f[2]); px = d[-w * h * 3:]
def rgb(x, y):
    i = (int((y + 0.5) * h / 100) * w + int((x + 0.5) * w / 200)) * 3
    return tuple(px[i:i + 3])
def at(x, y):
    r, g, b = rgb(x, y)
    half = r == 255 and g == b and 110 <= g <= 145  # 50% red over white; drawn twice it's about 64
    return {(255, 0, 0): "red", (0, 0, 255): "blue"}.get((r, g, b), "half red" if half else "other")
checks = [((70, 90), "blue", "inside the area"), ((70, 5), "other", "above the area"),
          ((35, 50), "red", "on the line"), ((70, 18), "red", "the line's mitred corner (no gap)"),
          ((165, 32), "red", "the spike's corner"), ((165, 25), "other", "above the spike (cut, no spike)"),
          ((185, 32), "half red", "a translucent spike's corner"), ((185, 45), "half red", "inside a translucent spike")]
bad = [f"{name} at {xy}: {at(*xy)} {rgb(*xy)}, expected {want}" for xy, want, name in checks if at(*xy) != want]
print("\n".join(bad) if bad else "lines and areas: all %d pixels as expected (%dx%d)" % (len(checks), w, h))
sys.exit(1 if bad else 0)
EOF
cp "$work/Backend.cs.good" "$work/Win/Backend.cs"; cp "$work/Main.qml.good" "$work/Win/Main.qml"

step "Theme.Name (C#) and theme.name (QML) read current/theme.name, and follow a switch"
state=$work/theme-home/.local/state/omarchy/current; mkdir -p "$state/theme"; echo tokyo-night > "$state/theme.name"
sed -i '/^public class Backend : BackendBase$/{n;a\
    public Backend() => Theme.Changed += () => Notify(nameof(ThemeName));\
    public string ThemeName => Theme.Name;
}' "$work/Win/Backend.cs"
cat > "$work/Win/Main.qml" <<'EOF'
import QtQuick

Window {
  visible: true
  width: 400
  height: 300
  Column {
    Text { text: "qml: " + theme.name }
    Text { text: "cs: " + backend.themeName }
  }
}
EOF
(cd "$work/Win" && sh .agents/skills/omasharp/scripts/check.sh --offscreen --no-tests --env "HOME=$work/theme-home") >"$work/win-theme.log" 2>&1 || { tail -20 "$work/win-theme.log"; fail "theme name app"; }
{ grep -q "qml: tokyo-night" "$work/Win/obj/shot.txt" && grep -q "cs: tokyo-night" "$work/Win/obj/shot.txt"; } || { cat "$work/Win/obj/shot.txt"; fail "theme name: not read"; }
# The same build, run by hand: switch the theme the way omarchy-theme-set does (theme.name rewritten).
set --; while IFS= read -r a; do set -- "$@" "$a"; done < "$work/Win/obj/omasharp/launch"
t=$work/theme.txt
HOME=$work/theme-home QT_QPA_PLATFORM=offscreen QT_QUICK_BACKEND=software OMASHARP_TEXT_DUMP="$t" timeout 20 "$@" >"$work/theme-run.log" 2>&1 &
pid=$!
for _ in $(seq 100); do grep -q "cs: tokyo-night" "$t" 2>/dev/null && break; sleep 0.1; done
echo catppuccin > "$state/theme.name"
for _ in $(seq 50); do grep -q "qml: catppuccin" "$t" && grep -q "cs: catppuccin" "$t" && break; sleep 0.1; done
kill "$pid" 2>/dev/null || true; wait "$pid" 2>/dev/null || true
cat "$t"
{ grep -q "qml: catppuccin" "$t" && grep -q "cs: catppuccin" "$t"; } || { tail -20 "$work/theme-run.log"; fail "theme name: didn't follow the switch"; }
cp "$work/Backend.cs.good" "$work/Win/Backend.cs"; cp "$work/Main.qml.good" "$work/Win/Main.qml"

step "check.sh fails when the app's QML throws while running"
cp "$work/Win/Main.qml" "$work/Main.qml.good"
sed -i '0,/id: win/s//id: win\n  Component.onCompleted: notDefinedAnywhere.call()/' "$work/Win/Main.qml"
(cd "$work/Win" && sh .agents/skills/omasharp/scripts/check.sh --offscreen --no-tests) >"$work/win-qmlerr.log" 2>&1 && { tail -20 "$work/win-qmlerr.log"; fail "check.sh passed an app whose QML threw"; }
grep -A1 -- "--- QML errors ---" "$work/win-qmlerr.log"
grep -q "ReferenceError: notDefinedAnywhere is not defined" "$work/win-qmlerr.log" || { tail -20 "$work/win-qmlerr.log"; fail "check.sh: the QML error isn't shown"; }
cp "$work/Main.qml.good" "$work/Win/Main.qml"

step "panel app: build, tests, AOT publish, stdio protocol"
new -n Pan --kind panel
[ ! -e "$work/Pan/.github" ] || fail "panel app: has the window apps' release workflow"
# Names that are Visual Basic keywords build: the app is C# only (CA1716 is off in .editorconfig).
printf '%s\n' 'public record Step(int Index);' 'public interface ICursor { void Next(); }' > "$work/Pan/Keywords.cs"
(cd "$work/Pan" && dotnet build -v quiet -nologo && dotnet run --project tests && dotnet publish -v quiet -nologo) >"$work/pan.log" 2>&1 || { tail -30 "$work/pan.log"; fail "panel app"; }
grep -E "warning IL[0-9]+" "$work/pan.log" && fail "panel app: AOT warnings"
[ -s "$work/Pan/plugin/Backend.qml" ] || fail "panel app: no generated Backend.qml"
reply=$( (printf '%s\n' '{"op":"set","name":"count","value":0}' '{"op":"call","id":1,"name":"increment","args":[]}' '{"op":"get","id":2,"name":"label"}'; sleep 0.5) | "$work/Pan/plugin/bin/Pan")
echo "$reply"
echo "$reply" | grep -q '"op":"hello"' || fail "panel: no hello"
echo "$reply" | grep -q '"op":"notify","name":"count","value":0' || fail "panel: set didn't echo the value C# kept"
echo "$reply" | grep -q '"value":"Counted 1 time"' || fail "panel: increment didn't reach label"

step "every omasharp rule fails a generated app's build, with its message"
new -n Lint
# One mistake per rule (OMASHARP001-010), plus a method CA1822 would want static.
cat > "$work/lint.cs" <<'EOF'
    public void Delete() { }
    public int Total => 1;
    public void total() { }
    public int Field;
    public string Status { get; private set; } = "";
    public void Load() => Status = "loaded";
    readonly List<string> _items = new();
    public int ItemCount => _items.Count;
    public void AddItem(string s) => _items.Add(s);
    public void Touch() => Notify("Nope");
    public record Item(string Name);
    public void Pick(Item item) { }
    public ValueTask Save() => default;
    public async void Go() { await Task.Delay(1); }
    public async Task Off() { await Task.Delay(1).ConfigureAwait(false); }
    public int Same(int a) => a;
EOF
sed -i "/^public class Backend : BackendBase\$/{n;r $work/lint.cs
}" "$work/Lint/Backend.cs"
if (cd "$work/Lint" && dotnet build -v quiet -nologo) >"$work/lint.log" 2>&1; then fail "lint: build succeeded"; fi
for id in 001 002 003 004 005 006 007 008 009 010; do
  grep -q "error OMASHARP$id:" "$work/lint.log" || { grep -E "error|warning" "$work/lint.log" | sort -u; fail "lint: OMASHARP$id didn't fire"; }
done
grep -q "CA1822" "$work/lint.log" && fail "lint: CA1822 fired on a backend method (the suppressor should turn it off)"
grep -q "Backend.Delete becomes 'delete'" "$work/lint.log" || fail "lint: no reserved-name message"
echo "OMASHARP001-010 each fail the build"

step "without the analyzers, a name QML can't take still fails the panel build (--qml-shim)"
sed -i 's/public void Reset()/public void Delete() { }\n    public void Reset()/' "$work/Pan/Backend.cs"
if (cd "$work/Pan" && dotnet build -v quiet -nologo -p:OmasharpAnalyzers=false -p:OmasharpWarningsAsErrors=false) >"$work/pan-reserved.log" 2>&1; then fail "reserved name: build succeeded"; fi
grep -q "Backend.Delete becomes 'delete'" "$work/pan-reserved.log" || { tail -20 "$work/pan-reserved.log"; fail "reserved name: no message"; }

step "bridge + skill update on a fresh app changes nothing"
out=$(cd "$work/Win" && sh bridge/update.sh .)
echo "$out" | tail -1
echo "$out" | grep -q " 0 updated, 0 added, 0 removed" || fail "update: unexpected changes"

step "SQLite as the skill's references/sqlite.md says: window app (AOT, -t:Install, kept across runs), panel"
ref=$root/template/.agents/skills/omasharp/references/sqlite.md
# The reference's own code: its n-th unindented code block in <lang>.
block() { awk -v lang="$1" -v n="$2" '$0 == "```" lang { i++; on = (i == n); next } /^```$/ { on = 0 } on' "$ref"; }
block xml 1 > "$work/sqlite-refs.xml"
for app in Notes NotesPanel; do
  if [ $app = Notes ]; then new -n $app; else new -n $app --kind panel; fi
  block csharp 1 > "$work/$app/Db.cs"; block csharp 2 > "$work/$app/Backend.cs"
  awk -v refs="$work/sqlite-refs.xml" '/<Import Project=/ { while ((getline l < refs) > 0) print l } 1' "$work/$app/$app.csproj" > "$work/csproj" && mv "$work/csproj" "$work/$app/$app.csproj"
done
# Every start adds a note, so the second start shows both.
cat > "$work/Notes/Main.qml" <<'EOF'
import QtQuick
import QtQuick.Controls

ApplicationWindow {
  visible: true
  width: 400
  height: 200
  Label { text: "Notes: " + backend.notes.map(n => n.text).join(", ") }
  Component.onCompleted: backend.add("run " + (backend.notes.length + 1))
}
EOF
# Installed into a private HOME, where the app also keeps its database; dotnet keeps the real one.
h=$work/home; mkdir -p "$h"; real_home=$HOME; cache=${XDG_CACHE_HOME:-$HOME/.cache}
(cd "$work/Notes" && HOME=$h DOTNET_CLI_HOME=$real_home NUGET_PACKAGES=$nuget_cache XDG_CACHE_HOME=$cache dotnet publish -t:Install -v quiet -nologo) >"$work/notes.log" 2>&1 || { tail -30 "$work/notes.log"; fail "sqlite window app: publish -t:Install"; }
files=$(cd "$h/.local/share/omasharp/notes" && echo *)
[ "$files" = "libnotes.so notes notes.rcc" ] || fail "sqlite window app: installed $files, not just its own three files"
shown() {
  t=$work/notes.txt; rm -f "$t"
  env -u XDG_DATA_HOME HOME="$h" QT_QPA_PLATFORM=offscreen QT_QUICK_BACKEND=software OMASHARP_TEXT_DUMP="$t" timeout 15 "$h/.local/bin/notes" >>"$work/notes-run.log" 2>&1 &
  pid=$!
  for _ in $(seq 100); do [ -s "$t" ] && break; sleep 0.1; done
  sleep 0.5; kill "$pid" 2>/dev/null || true; wait "$pid" 2>/dev/null || true
  cat "$t" 2>/dev/null || true
}
first=$(shown); second=$(shown); echo "$first"; echo "$second"
[ "$first" = "Label: Notes: run 1" ] && [ "$second" = "Label: Notes: run 1, run 2" ] || { tail -20 "$work/notes-run.log"; fail "sqlite window app: notes not shown or not kept"; }
[ -s "$h/.local/share/notes/app.db" ] || fail "sqlite window app: no database in ~/.local/share/notes/"
(cd "$work/NotesPanel" && dotnet publish -v quiet -nologo) >"$work/notes-panel.log" 2>&1 || { tail -30 "$work/notes-panel.log"; fail "sqlite panel: publish"; }
files=$(cd "$work/NotesPanel/plugin/bin" && echo *)
[ "$files" = NotesPanel ] || fail "sqlite panel: plugin/bin has $files, not just the backend"
d=$work/panel-data
echo '{"op":"call","id":1,"name":"add","args":["milk"]}' | XDG_DATA_HOME=$d "$work/NotesPanel/plugin/bin/NotesPanel" >/dev/null
reply=$(XDG_DATA_HOME=$d "$work/NotesPanel/plugin/bin/NotesPanel" </dev/null)
echo "$reply"
echo "$reply" | grep -q '"notes":\[{"id":1,"text":"milk"}\]' || fail "sqlite panel: note not kept across runs"
[ -s "$d/notespanel/app.db" ] || fail "sqlite panel: no database in \$XDG_DATA_HOME/notespanel/"
# Unit tests set up as the reference says: ../Db.cs and the packages in tests/, AppInfo declared there.
t=$work/NotesPanel/tests
sed -i 's#<Compile Include="../Backend.cs;#<Compile Include="../Backend.cs;../Db.cs;#' "$t"/*.csproj
awk -v refs="$work/sqlite-refs.xml" '/^<\/Project>/ { while ((getline l < refs) > 0) print l } 1' "$t"/*.csproj > "$work/csproj" && mv "$work/csproj" "$t"/*.csproj
echo 'namespace Omasharp { static class AppInfo { public const string Id = "tests"; } }' > "$t/AppInfo.cs"
cat > "$t/Tests.cs" <<'EOF'
global using AppBackend = Backend;

var failures = 0;
void Check(string name, bool ok)
{
    Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {name}");
    if (!ok) failures++;
}
static bool Works(Action a) { try { a(); return true; } catch (InvalidOperationException e) { Console.WriteLine($"     {e.Message}"); return false; } }
var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");

using (var b = new Backend(path)) { b.Add("milk"); b.Add("eggs"); b.Remove(b.Notes[0].Id); }
using (var b = new Backend(path)) Check("notes are kept", b.Notes is [{ Text: "eggs" }]);

// Two copies of the app open: a write waits for the other copy's transaction instead of failing.
using (var first = new Db(path))
using (var second = new Db(path))
{
    first.Exec("BEGIN IMMEDIATE");
    var commit = Task.Run(async () => { await Task.Delay(300); first.Exec("COMMIT"); });
    Check("a write waits for another connection's transaction", Works(() => second.Run("INSERT INTO notes(text) VALUES (?)", "tea")));
    commit.Wait();
}

// Two copies of the app start at once on a new database: the second waits, then skips the steps.
var fresh = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
using (var other = new Db(fresh))
{
    other.Exec("BEGIN IMMEDIATE");
    var starting = Task.Run(() => { using var b = new Backend(fresh); });
    await Task.Delay(300);
    other.Exec("CREATE TABLE notes(id INTEGER PRIMARY KEY, text TEXT NOT NULL); PRAGMA user_version = 1; COMMIT");
    Check("a second copy starting at the same time migrates once", Works(() => starting.GetAwaiter().GetResult()));
}
foreach (var f in new[] { fresh, fresh + "-wal", fresh + "-shm" }) File.Delete(f);

// A migration that fails halfway leaves neither the change nor the new version behind.
using (var db = new Db(path))
{
    Works(() => db.Transaction(() => db.Exec("ALTER TABLE notes ADD COLUMN done INTEGER NOT NULL DEFAULT 0; ALTER TABLE nope ADD COLUMN x; PRAGMA user_version = 2")));
    var columns = db.Query("SELECT count(*) FROM pragma_table_info('notes') WHERE name = 'done'", r => SQLitePCL.raw.sqlite3_column_int(r, 0))[0];
    var version = db.Query("PRAGMA user_version", r => SQLitePCL.raw.sqlite3_column_int(r, 0))[0];
    Check("a failed transaction changes nothing", columns == 0 && version == 1);
    Check("the connection works after it", Works(() => db.Transaction(() => db.Run("INSERT INTO notes(text) VALUES (?)", "jam"))));
}

foreach (var f in new[] { path, path + "-wal", path + "-shm" }) File.Delete(f);
Console.WriteLine(failures == 0 ? "all passed" : $"{failures} failed");
return failures == 0 ? 0 : 1;
EOF
(cd "$work/NotesPanel" && dotnet run --project tests) >"$work/notes-tests.log" 2>&1 || { tail -30 "$work/notes-tests.log"; fail "sqlite panel: unit tests"; }
grep -E "^(ok|FAIL)" "$work/notes-tests.log"

step "Install with the app's own app.desktop, then Uninstall (the same private HOME)"
cat > "$work/Notes/app.desktop" <<'EOF'
[Desktop Entry]
Type=Application
Name=Notes
Comment=Notes kept in SQLite
Exec=@EXEC@ %f
Icon=wrong
StartupWMClass=wrong
Terminal=false
Categories=Utility;
MimeType=text/plain;
Actions=new;

[Desktop Action new]
Name=New note
Exec=@EXEC@ --new
EOF
printf '<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16"/>\n' > "$work/Notes/icon.svg"
as_home() { (cd "$work/Notes" && HOME=$h DOTNET_CLI_HOME=$real_home NUGET_PACKAGES=$nuget_cache XDG_CACHE_HOME=$cache "$@" -v quiet -nologo); }
as_home dotnet publish -t:Install >"$work/desktop.log" 2>&1 || { tail -30 "$work/desktop.log"; fail "app.desktop: publish -t:Install"; }
entry=$h/.local/share/applications/notes.desktop; exe=$h/.local/share/omasharp/notes/notes
cat "$entry"
for line in "Exec=\"$exe\" %f" "Exec=\"$exe\" --new" "StartupWMClass=notes" "Icon=notes" "Comment=Notes kept in SQLite" ""; do
  grep -qxF -- "$line" "$entry" || fail "app.desktop: no line '$line' in the installed entry"
done
grep -q "wrong\|@EXEC@" "$entry" && fail "app.desktop: Install didn't set Icon, StartupWMClass or Exec"
if command -v update-desktop-database >/dev/null; then
  grep -q "text/plain=notes.desktop" "$h/.local/share/applications/mimeinfo.cache" || fail "app.desktop: MIME cache not refreshed"
fi
# An invalid entry fails Install and leaves the installed one as it was.
if command -v desktop-file-validate >/dev/null; then
  cp "$entry" "$work/entry.good"
  sed -i 's/^Type=Application$/Type=Nonsense/' "$work/Notes/app.desktop"
  as_home dotnet publish -t:Install >"$work/desktop-bad.log" 2>&1 && fail "app.desktop: an invalid entry was installed"
  { grep -q 'app.desktop.*value "Nonsense" for key "Type"' "$work/desktop-bad.log" && grep -q "app.desktop.*not a valid desktop entry" "$work/desktop-bad.log"; } || { tail -20 "$work/desktop-bad.log"; fail "app.desktop: no message for an invalid entry"; }
  cmp -s "$entry" "$work/entry.good" || fail "app.desktop: a failed Install changed the installed entry"
fi
as_home dotnet build -t:Uninstall >>"$work/desktop.log" 2>&1 || { tail -30 "$work/desktop.log"; fail "app.desktop: -t:Uninstall"; }
left=$(cd "$h/.local" && find bin share/applications share/icons share/omasharp ! -type d 2>/dev/null || true)
[ -z "$left" ] || fail "app.desktop: Uninstall left $left"

step "A release: -t:Package, then its install.sh as users run it (piped, the same private HOME)"
# Restored after the invalid-entry check; the name tests the quoting install.sh gets.
sed -i 's/^Type=Nonsense$/Type=Application/' "$work/Notes/app.desktop"
rel=$work/Notes/artifacts
as_home dotnet publish -t:Package -p:AppRepository=example/notes "-p:AppName=Jo's & Al's Notes" >"$work/package.log" 2>&1 || { tail -30 "$work/package.log"; fail "package: -t:Package"; }
made=$(cd "$rel" && printf "%s\n" * | LC_ALL=C sort | tr '\n' ' ')
[ "$made" = "SHA256SUMS install.sh notes-linux-x64.tar.gz " ] || fail "package: artifacts/ has $made"
(cd "$rel" && sha256sum -c --quiet SHA256SUMS) || fail "package: SHA256SUMS doesn't match"
listed=$(tar -tzf "$rel/notes-linux-x64.tar.gz" | grep -v '/$' | LC_ALL=C sort | tr '\n' ' ')
[ "$listed" = "notes/app/libnotes.so notes/app/notes notes/app/notes.rcc notes/install.sh notes/notes.desktop notes/notes.svg notes/version " ] || fail "package: the tarball has $listed"
grep -qxF "id='notes' name='Jo'\\''s & Al'\\''s Notes' repo='example/notes'" "$rel/install.sh" || fail "package: install.sh doesn't have the app's values"
# Users' way: curl … | sh. The release's files come from file:// here instead of github.com.
user_install() { curl -fsSL "file://$rel/install.sh" | env HOME="$h" OMASHARP_RELEASE_URL="file://${2:-$rel}" sh ${1:+-s "$1"}; }
user_install >"$work/user-install.log" 2>&1 || { cat "$work/user-install.log"; fail "release: install.sh"; }
cat "$work/user-install.log"
grep -q "^Installed Jo's & Al's Notes 0.1.0" "$work/user-install.log" || fail "release: no install message"
[ "$(cd "$h/.local/share/omasharp/notes" && echo *)" = "libnotes.so notes notes.rcc" ] || fail "release: installed $(cd "$h/.local/share/omasharp/notes" && echo *)"
[ "$(readlink "$h/.local/bin/notes")" = "$exe" ] || fail "release: ~/.local/bin/notes isn't the installed binary"
[ -f "$h/.local/share/icons/hicolor/scalable/apps/notes.svg" ] || fail "release: no icon"
for line in "Exec=\"$exe\" %f" "Exec=\"$exe\" --new" "StartupWMClass=notes" "Icon=notes"; do
  grep -qxF -- "$line" "$entry" || fail "release: no line '$line' in the installed entry"
done
case $(shown) in "Label: Notes: run 1, run 2, run 3"*) ;; *) tail -20 "$work/notes-run.log"; fail "release: the installed app didn't run" ;; esac
# Piped from inside an unpacked (older) release, it still downloads the latest.
mkdir -p "$work/oldrel"; tar -xzf "$rel/notes-linux-x64.tar.gz" -C "$work/oldrel"; echo 0.0.9 > "$work/oldrel/notes/version"
(cd "$work/oldrel/notes" && user_install) >"$work/inside.log" 2>&1 || { cat "$work/inside.log"; fail "release: piped install inside an unpacked release"; }
grep -q "^Installed Jo's & Al's Notes 0.1.0" "$work/inside.log" || { cat "$work/inside.log"; fail "release: piped install took the unpacked release in the current folder"; }
# A download that doesn't match SHA256SUMS installs nothing.
cp -r "$rel" "$work/bad"; printf x >> "$work/bad/notes-linux-x64.tar.gz"; cp "$exe" "$work/exe.before"
user_install "" "$work/bad" >"$work/bad.log" 2>&1 && fail "release: a download that doesn't match SHA256SUMS was installed"
grep -q "doesn't match the release's SHA256SUMS; nothing was installed" "$work/bad.log" || { cat "$work/bad.log"; fail "release: no message for a bad download"; }
cmp -s "$exe" "$work/exe.before" || fail "release: a bad download changed the installed app"
# Libraries older than the build's (a fake ldd says so): nothing is installed.
mkdir -p "$work/oldlibs"; printf '#!/bin/sh\necho "\t%s: version Qt_6.99 not found (required by %s)"\n' libQt6Core.so.6 notes > "$work/oldlibs/ldd"; chmod +x "$work/oldlibs/ldd"
(cd "$work" && tar -xzf "$rel/notes-linux-x64.tar.gz")
env HOME="$work/home2" PATH="$work/oldlibs:$PATH" sh "$work/notes/install.sh" >"$work/old.log" 2>&1 && fail "release: installed on libraries that are too old"
grep -q "Update the system" "$work/old.log" || { cat "$work/old.log"; fail "release: no message for old libraries"; }
[ ! -e "$work/home2" ] || fail "release: too-old libraries still wrote to HOME"
# Again (an update), then uninstall: nothing left. The update leaves apps called notes.new and notes.old alone.
for other in notes.new notes.old; do mkdir -p "$h/.local/share/omasharp/$other" && touch "$h/.local/share/omasharp/$other/libnotes.so"; done
user_install >>"$work/user-install.log" 2>&1 || fail "release: install.sh over an installed copy"
for other in notes.new notes.old; do
  [ -f "$h/.local/share/omasharp/$other/libnotes.so" ] || fail "release: installing notes removed the app $other"
  rm -r "${h:?}/.local/share/omasharp/$other"
done
# While another install of the app holds its lock, the next one waits for it.
( flock 9; touch "$work/locked"; while [ ! -e "$work/unlock" ]; do sleep 0.1; done ) 9>"$h/.local/share/omasharp/.notes.lock" &
holder=$!
while [ ! -e "$work/locked" ]; do sleep 0.05; done
user_install >"$work/wait.log" 2>&1 &
waiter=$!
for _ in $(seq 100); do grep -q "Waiting for another install" "$work/wait.log" && break; sleep 0.1; done
if ! grep -q "Waiting for another install" "$work/wait.log" || ! kill -0 "$waiter" 2>/dev/null; then
  touch "$work/unlock"; cat "$work/wait.log"; fail "release: an install didn't wait for the one holding the lock"
fi
touch "$work/unlock"; wait "$holder"; wait "$waiter" || { cat "$work/wait.log"; fail "release: the install that waited failed"; }
user_install uninstall >>"$work/user-install.log" 2>&1 || fail "release: install.sh uninstall"
left=$(cd "$h/.local" && find bin share/applications share/icons share/omasharp ! -type d 2>/dev/null || true)
[ -z "$left" ] || fail "release: uninstall left $left"
# Panels have no release.
(cd "$work/NotesPanel" && dotnet publish -t:Package -v quiet -nologo) >"$work/package-panel.log" 2>&1 && fail "package: a panel made a release"
grep -q "Package makes window app releases" "$work/package-panel.log" || { tail -10 "$work/package-panel.log"; fail "package: no message for a panel"; }

step "examples, through check.sh"
(cd "$root/examples/omacalc" && $check --offscreen --release) >"$work/omacalc.log" 2>&1 || { tail -30 "$work/omacalc.log"; fail "omacalc"; }
grep "passed" "$work/omacalc.log"
grep -q "AC" "$work/omacalc.log" || fail "omacalc: keypad not shown"
(cd "$root/examples/hello" && $check --offscreen --release) >"$work/hello.log" 2>&1 || { tail -30 "$work/hello.log"; fail "hello"; }
grep -q "Hello, Omarchy" "$work/hello.log" || fail "hello: greeting not shown"
(cd "$root/examples/hello" && dotnet publish -p:OmarchyKind=panel -v quiet -nologo) >"$work/hello-panel.log" 2>&1 || { tail -30 "$work/hello-panel.log"; fail "hello panel publish"; }

step "all good"
