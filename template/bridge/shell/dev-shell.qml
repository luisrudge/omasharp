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
