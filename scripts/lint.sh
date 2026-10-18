#!/bin/sh
# lint.sh [--staged]: the formatting and lint checks CI runs: C# (dotnet format whitespace),
# C++ (clang-format, style in .clang-format) and shell scripts (shellcheck). --staged checks only
# the files staged for commit (the pre-commit hook). Outside CI a missing tool is skipped.
set -u
cd "$(dirname "$0")/.." || exit 1
if [ "${1:-}" = --staged ]; then
  files() { git diff --cached --name-only --diff-filter=ACMR -- "$@"; }
else
  files() { git ls-files -- "$@"; }
fi
fail=0
have() {
  command -v "$1" >/dev/null 2>&1 && return 0
  if [ -n "${CI:-}" ]; then echo "lint: $1 is not installed" >&2; fail=1; else echo "lint: no $1, skipping it" >&2; fi
  return 1
}
# Repo paths have no spaces, so the file lists below are split on purpose.
cs=$(files '*.cs')
if [ -n "$cs" ] && have dotnet; then
  # shellcheck disable=SC2086
  dotnet format whitespace --folder --verify-no-changes --include $cs ||
    { echo "lint: C# formatting is off. Fix: dotnet format whitespace --folder" >&2; fail=1; }
fi
cpp=$(files '*.cpp' '*.h')
if [ -n "$cpp" ] && have clang-format; then
  # shellcheck disable=SC2086
  clang-format --dry-run -Werror $cpp ||
    { echo "lint: C++ formatting is off. Fix: clang-format -i FILE" >&2; fail=1; }
fi
sh=$(files '*.sh' .githooks/pre-commit)
if [ -n "$sh" ] && have shellcheck; then
  # shellcheck disable=SC2086
  shellcheck -x $sh || fail=1
fi
exit $fail
