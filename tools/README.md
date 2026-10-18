# tools/

Helpers for benchmarking on Hyprland. None of them show anything on your screen or add a monitor to your session.

| | |
|---|---|
| `bench.sh LABEL CMD [ARGS]` | One run of an app on a hidden special workspace. Prints time to window (mapped), RSS/PSS, threads and processes of the whole tree, idle CPU and wake-ups over 10 s (`BENCH_IDLE=0` skips them), and close time. Alternate apps across runs, e.g. `for i in 1 2 3 4 5 6 7; do sh tools/bench.sh a ./a; sh tools/bench.sh b ./b; done > runs.txt`. |
| `summarize.py RUNS.txt [LABEL...]` | Medians and ranges from `bench.sh` output, as a Markdown table. |
| `session.sh` | Sourced by `bench.sh`: finds your Hyprland session even when the environment points at a stale or nested one. |

Apps don't need these: the skill's `check.sh` (`template/.agents/skills/omasharp/scripts/`) covers checking an app.
