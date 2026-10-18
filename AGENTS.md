# omasharp: notes for coding agents

This repo is the `omarchy-app` dotnet template and its bridge (C# ⟷ QML for Omarchy). Read @CONTRIBUTING.md for layout and rules, and @docs/architecture.md before changing `template/bridge/`.

- Full check: `sh scripts/test.sh` (needs the .NET SDK, Qt 6, gcc, clang). Run it before you say you're done.
- Apps and examples are written the way the skill in `template/.agents/skills/omasharp/` says (`SKILL.md`, `references/`); it's what agents working on a generated app read, so keep it accurate when you change behavior.
- `analyzers/`: rules fail every app's build, so each must be precise; add the quiet cases to `analyzers/Omasharp.Analyzers.Tests` too.
- Never open windows on the user's screen. Use the skill's `scripts/check.sh` in an app or example (private nested compositor), or Qt's offscreen platform.
- Never add a monitor to the user's Hyprland session, and never send keys or clipboard data to it.
- Bridge changes: keep `template/bridge/host/omasharp.h` and `template/bridge/cs/Native.cs` in sync, and bump `template/bridge/VERSION`.
- Nothing machine-specific in committed files: no home paths, user names, host names or instance ids.
