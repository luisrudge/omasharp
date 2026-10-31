import QtQuick
import Quickshell
import Quickshell.Wayland
import qs.Commons
import qs.Commons as Commons
import qs.Ui

Item {
  id: root

  property var shell: null
  property var manifest: null
  property bool opened: false
  property string screenName: ""

  function open(payloadJson) {
    try { screenName = JSON.parse(payloadJson || "{}").screen || "" } catch (e) { screenName = "" }
    opened = true
  }
  function close() { opened = false }
  function dismiss() { shell ? shell.hide(manifest.id) : close() }

  Backend { id: backend }

  PanelWindow {
    visible: root.opened
    screen: {
      for (var i = 0; i < Quickshell.screens.length; i++)
        if (Quickshell.screens[i].name === root.screenName) return Quickshell.screens[i]
      return null
    }
    anchors { top: true; bottom: true; left: true; right: true }
    color: "transparent"
    exclusionMode: ExclusionMode.Ignore
    WlrLayershell.namespace: "omarchy-app-id"
    WlrLayershell.layer: WlrLayer.Overlay
    WlrLayershell.keyboardFocus: root.opened ? WlrKeyboardFocus.Exclusive : WlrKeyboardFocus.None

    Rectangle {
      anchors.fill: parent
      color: Commons.Color.menu.scrim
      MouseArea { anchors.fill: parent; onClicked: root.dismiss() }
    }

    Item {
      anchors.fill: parent
      focus: true
      Keys.onEscapePressed: root.dismiss()
      Keys.onReturnPressed: backend.running ? backend.increment() : backend.restart()
      Keys.onPressed: function(event) {
        if (event.key === Qt.Key_R && (event.modifiers & Qt.ControlModifier)) { backend.reset(); event.accepted = true }
      }

      Rectangle {
        anchors.centerIn: parent
        width: content.implicitWidth + Style.space(64)
        height: content.implicitHeight + Style.space(48)
        color: Commons.Color.popups.background
        border.color: Commons.Color.popups.border
        border.width: 2
        radius: Style.cornerRadius
        MouseArea { anchors.fill: parent; onClicked: {} }

        Column {
          id: content
          anchors.centerIn: parent
          spacing: Style.space(20)

          Text {
            anchors.horizontalCenter: parent.horizontalCenter
            textFormat: Text.PlainText
            text: backend.ready ? backend.label : (backend.running ? "Starting…" : "Backend stopped: " + backend.lastError)
            color: Commons.Color.popups.text
            font.family: Style.font.family
            font.pixelSize: Style.font.display
          }

          Row {
            anchors.horizontalCenter: parent.horizontalCenter
            spacing: Style.space(12)
            Button { text: backend.running ? "Count" : "Restart"; focusable: true; bordered: true; onClicked: backend.running ? backend.increment() : backend.restart() }
            Button { text: "Reset"; focusable: true; bordered: true; onClicked: backend.reset() }
            Button { text: "Close"; focusable: true; bordered: true; onClicked: root.dismiss() }
          }
        }
      }
    }
  }
}
