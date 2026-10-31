import QtQuick
import QtQuick.Controls

ApplicationWindow {
  id: win
  width: 420
  height: 300
  visible: true
  title: "OmarchyApp"
  color: theme.background

  font.family: "monospace"

  Column {
    anchors.centerIn: parent
    spacing: 20

    Label {
      anchors.horizontalCenter: parent.horizontalCenter
      text: backend.label
      color: theme.foreground
      font.pixelSize: 22
    }

    Row {
      anchors.horizontalCenter: parent.horizontalCenter
      spacing: 12
      AppButton { text: "Count"; accent: true; onClicked: backend.increment() }
      AppButton { text: "Reset"; onClicked: backend.reset() }
    }
  }

  Shortcut { sequence: "Ctrl+Return"; onActivated: backend.increment() }
  Shortcut { sequence: "Ctrl+R"; onActivated: backend.reset() }
  Shortcut { sequence: "Ctrl+Q"; onActivated: win.close() }

  component AppButton: Rectangle {
    id: button
    property string text
    property bool accent: false
    signal clicked()
    width: 120; height: 40
    color: mouse.pressed ? (accent ? Qt.darker(theme.accent, 1.2) : theme.selection) : fill
    readonly property color fill: accent ? theme.accent : theme.lighterBackground
    border.color: theme.muted
    border.width: accent ? 0 : 1
    Label { anchors.centerIn: parent; text: button.text; color: button.accent ? theme.background : theme.foreground; font.pixelSize: 15 }
    MouseArea { id: mouse; anchors.fill: parent; onClicked: button.clicked() }
  }
}
