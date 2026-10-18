# hello

One `Backend.cs` used two ways. On the left it's a window app, compiled with native AOT. On the right it's an omarchy-shell panel, with the C# running as a child process.

![hello as a window app and as a panel](screenshot.png)

```sh
dotnet watch                                                               # the window app, with live QML and C# edits
sh ../../template/.agents/skills/omasharp/scripts/check.sh                 # checks the window app off-screen
OmarchyKind=panel sh ../../template/.agents/skills/omasharp/scripts/check.sh   # checks the panel
dotnet publish -p:OmarchyKind=panel -t:Install                             # installs the panel
omarchy-shell shell toggle omasharp-hello                                  # opens it
```

It also tests the bridge's edge cases:

- `CountSlowly` is an async method.
- `Fail` throws.
- `Time` updates from a timer on a background thread.
- `Crash` kills the backend on purpose. The panel survives and can restart it.

It uses the repo's `../../template/bridge` directly. Apps made from the template get their own copy instead.
