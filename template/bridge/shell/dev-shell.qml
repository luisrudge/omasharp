// Panel dev harness (dotnet run and check.sh on a panel app): shows plugin/Panel.qml
// in its own Quickshell process, with omarchy-shell's theme (qs.Commons) but none of
// its risk. Closing the panel ends the run.
import QtQuick
import Quickshell
import "plugin" as P

ShellRoot {
  P.Panel {
    id: panel
    Component.onCompleted: open(Quickshell.env("OMASHARP_PANEL_PAYLOAD") || "{}")
    onOpenedChanged: if (!opened) Qt.quit()
  }

  TextDump { roots: [panel] }
}
