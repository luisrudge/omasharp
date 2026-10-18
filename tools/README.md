# tools/

Benchmark helpers for contributors. None of them show anything on your screen or add a monitor to your session. Apps don't need them. The skill's `check.sh` (in `template/.agents/skills/omasharp/scripts/`) is what checks an app.

| Script | What it does |
|---|---|
| `bench.sh LABEL CMD [ARGS]` | Runs an app once on a hidden special workspace. Prints time to window, RSS and PSS, threads and processes for the whole process tree, idle CPU and wake-ups over 10 s, and close time. `BENCH_IDLE=0` skips the idle sample. |
| `summarize.py RUNS.txt [LABEL...]` | Turns `bench.sh` output into a Markdown table of medians and ranges. |
| `session.sh` | `bench.sh` sources it to find your Hyprland session, even when the environment points at an old or nested one. |

Alternate the apps between runs, so changes in the machine's state hit each one evenly:

```sh
for i in 1 2 3 4 5 6 7; do sh tools/bench.sh a ./a; sh tools/bench.sh b ./b; done > runs.txt
python3 tools/summarize.py runs.txt a b
```
