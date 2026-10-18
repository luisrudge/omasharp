#!/bin/sh
# scripts/test.sh: the whole check, as CI runs it. Needs: dotnet SDK 10+, qmake6/g++ (qt6-base,
# qt6-declarative), clang (native AOT link). Uses a private template store and temp folders:
# it never touches your installed templates, your screen or your files.
#   1. the analyzers' unit tests, then pack them and the template (generated apps restore
#   Omasharp.Analyzers from that local package); 2. a new window app through the skill's check.sh
#   (unit tests, dev and native AOT builds, each run headless on Qt's offscreen platform, checking
#   the visible text); a new panel app: build, tests, AOT publish, its stdio protocol; 3. every
#   omasharp rule fails a generated app's build; 4. bridge update on a fresh app is a no-op;
#   5. the examples, through check.sh. Warnings fail every build (Omasharp.targets).
set -eu
root=$(cd "$(dirname "$0")/.." && pwd)
work=$(mktemp -d "${TMPDIR:-/tmp}/omasharp-test.XXXXXX")
# A version of the analyzers package for this run only, removed from the NuGet cache afterwards.
export OmasharpAnalyzersVersion="0.0.0-test.$$"
nuget_cache=$(dotnet nuget locals global-packages --list | sed 's/^global-packages: //')
trap 'rm -rf "$work" "${nuget_cache%/}/omasharp.analyzers/$OmasharpAnalyzersVersion"; rmdir "${nuget_cache%/}/omasharp.analyzers" 2>/dev/null || true' EXIT
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
export OMASHARP_TEMPLATE_HIVE="$work/hive"
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
for f in AGENTS.md CLAUDE.md .editorconfig .agents/skills/omasharp/SKILL.md .agents/skills/omasharp/scripts/check.sh bridge/MANIFEST; do
  [ -f "$work/Win/$f" ] || fail "window app: no $f"
done
grep -q "  .agents/skills/omasharp/SKILL.md$" "$work/Win/bridge/MANIFEST" || fail "MANIFEST doesn't cover the skill"

step "window app: check.sh (tests, dev build, offscreen run), then --release"
(cd "$work/Win" && sh .agents/skills/omasharp/scripts/check.sh --offscreen && sh .agents/skills/omasharp/scripts/check.sh --offscreen --release --no-tests) >"$work/win.log" 2>&1 || { tail -30 "$work/win.log"; fail "window app"; }
grep -A3 "visible text" "$work/win.log"
[ "$(grep -c "Nothing counted yet" "$work/win.log")" -eq 2 ] || fail "window app: expected text not shown in both builds"

step "check.sh fails when the app's QML throws while running"
cp "$work/Win/Main.qml" "$work/Main.qml.good"
sed -i '0,/id: win/s//id: win\n  Component.onCompleted: notDefinedAnywhere.call()/' "$work/Win/Main.qml"
(cd "$work/Win" && sh .agents/skills/omasharp/scripts/check.sh --offscreen --no-tests) >"$work/win-qmlerr.log" 2>&1 && { tail -20 "$work/win-qmlerr.log"; fail "check.sh passed an app whose QML threw"; }
grep -A1 -- "--- QML errors ---" "$work/win-qmlerr.log"
grep -q "ReferenceError: notDefinedAnywhere is not defined" "$work/win-qmlerr.log" || { tail -20 "$work/win-qmlerr.log"; fail "check.sh: the QML error isn't shown"; }
cp "$work/Main.qml.good" "$work/Win/Main.qml"

step "panel app: build, tests, AOT publish, stdio protocol"
new -n Pan --kind panel
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

step "examples, through check.sh"
(cd "$root/examples/omacalc" && $check --offscreen --release) >"$work/omacalc.log" 2>&1 || { tail -30 "$work/omacalc.log"; fail "omacalc"; }
grep "passed" "$work/omacalc.log"
grep -q "AC" "$work/omacalc.log" || fail "omacalc: keypad not shown"
(cd "$root/examples/hello" && $check --offscreen --release) >"$work/hello.log" 2>&1 || { tail -30 "$work/hello.log"; fail "hello"; }
grep -q "Hello, Omarchy" "$work/hello.log" || fail "hello: greeting not shown"
(cd "$root/examples/hello" && dotnet publish -p:OmarchyKind=panel -v quiet -nologo) >"$work/hello-panel.log" 2>&1 || { tail -30 "$work/hello-panel.log"; fail "hello panel publish"; }

step "all good"
