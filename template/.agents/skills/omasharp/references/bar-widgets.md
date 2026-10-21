# Bar widgets

A bar widget (`OmarchyKind` `bar-widget`) is an icon in the Omarchy bar that opens a dropdown, like Omarchy's own Tailscale and Power. It's a shell plugin like a panel ([panels.md](panels.md)): QML in omarchy-shell, `Backend.cs` in its own process. Its files are in `plugin/`:

| File | What it is |
|---|---|
| `BarWidget.qml` | The icon and the dropdown. The bar makes one per monitor. |
| `Service.qml` | Runs the backend once for the whole session and shares it with every monitor's icon. Leave it as is. |
| `manifest.json` | The plugin's id, name and description, and `barWidget` (`displayName`, `category`, `defaultSection`). |

## The icon

- `BarIconButton`'s `text` is the icon: a Nerd Font glyph (`"\uf0e7"`) or a few characters. The starter shows `backend.count`, so the icon changes when C# does. Keep it short: the bar is about 26 pixels tall.
- `active: root.opened` gives it the bar's active color while the dropdown is open. Bind `active` to backend state instead for an alert (a failed job, a new message).
- `tooltipText` shows on hover.
- `onPressed(button)` gets `Qt.LeftButton`, `Qt.RightButton` or `Qt.MiddleButton`. The starter opens the dropdown on a left click and counts on a right click. Omarchy's widgets open their dropdown with a left click, so keep that one.

## The dropdown

- `KeyboardPanel` places it next to the icon on any bar edge and closes it on a click outside or a second click on the icon (Escape closes it through `PanelKeyCatcher`, below). Size it with `contentWidth: panel.fittedContentWidth(Style.space(280))` and `contentHeight: panel.fittedContentHeight(column.implicitHeight)`, which keep it on the screen.
- `PanelKeyCatcher` turns keys into signals: `closeRequested` (Escape), `activateRequested` (Return, Space), `moveRequested(dx, dy)` (arrows and h/j/k/l), `deleteRequested` (x), `tabRequested(direction)` (Tab: `root.switchPanel(direction)` moves to the next bar widget's dropdown) and `textKey(text)` for other letters. It takes keys before the items inside it, so a `TextField` needs `blocked: field.activeFocus` on the catcher.
- Colors, fonts and the shell's controls are the same as a panel's ([look.md](look.md)).

## The backend's lifetime

- `Service.qml` starts the backend when the widget is enabled (Install does it) and runs it for the whole session, whether the dropdown is open or not. One backend serves every monitor's icon, so they all show the same state.
- `BarWidget.qml` gets it from the shell with `bar.shell.serviceFor(bar.pluginId)`. Until the shell hands it over, a stand-in (`idle`) that never runs answers, so `backend` is never null, and `connected` is false: the starter shows "Starting…" and doesn't offer Restart.
- It ends when the plugin is disabled or uninstalled, and when omarchy-shell restarts. Reinstalling restarts it, since the shell reloads the plugin. To keep state across those, save it to disk, as [panels.md](panels.md) says.
- Calls while the backend isn't running reject; check `backend.ready` before acting on input. `backend.running`, `backend.lastError` and `backend.restart()` are there for showing and recovering from a crash, as in a panel.

## Install and placement

- `dotnet publish -t:Install` copies `plugin/` to `~/.config/omarchy/plugins/<appid>/` and enables it, which puts the icon in the bar section `defaultSection` names in `manifest.json` (`right` in the starter).
- The sections, in the order they run along the bar: `left`, `centerBegin`, `center`, `centerEnd`, `right`, and `tray`, the tray's drawer. `center` is the clock's box, centered as one group: put a widget beside the clock with `centerBegin` or `centerEnd`, never `center`, or the clock moves off center (a `defaultSection` of `center` lands in `centerEnd`). Omarchy before 4.5 has only `left`, `center` and `right`, so `right` and `left` work everywhere.
- The user moves it by dragging it, or with `omarchy bar move <appid> --section <section>`. Reinstalling keeps it where it is.
- `dotnet build -t:Uninstall` takes it out of the bar and removes it.
- omarchy-shell may keep running the previous QML after a reinstall. If changes don't show, `omarchy-shell shell rescanPlugins`; if they still don't, they will after the next shell restart.

## Vertical bars

The bar can be on the left or right edge. Then `bar.vertical` is true and the bar is about 28 pixels wide: keep the icon to a glyph or one or two characters, and swap long text for a glyph when `bar.vertical`. `KeyboardPanel` opens the dropdown beside the bar.

## Checking it

`check.sh` shows the widget in a stand-in bar at the top of a private screen, with the dropdown open, and prints the icon's text first, then the dropdown's. `--keys` go to the dropdown; Escape closes it, which ends the run, so don't end with `{Esc}`.

```sh
sh .agents/skills/omasharp/scripts/check.sh --keys "{Enter}"                        # the icon and the dropdown both show 1
sh .agents/skills/omasharp/scripts/check.sh --env OMASHARP_BAR_POSITION=left        # a vertical bar (top, bottom, left, right)
```
