import QtQuick
import QtQuick.Controls

ApplicationWindow {
  id: win
  width: 420
  height: 360
  visible: true
  title: backend.countdown + " · " + backend.phase
  color: theme.background

  font.family: "monospace"

  Timer { interval: 200; repeat: true; running: backend.isRunning; onTriggered: backend.tick() }

  Column {
    anchors.centerIn: parent
    spacing: 16

    Label {
      anchors.horizontalCenter: parent.horizontalCenter
      text: backend.phase
      color: backend.onBreak ? Qt.alpha(theme.foreground, 0.6) : theme.accent
      font.pixelSize: 24
    }

    Label {
      anchors.horizontalCenter: parent.horizontalCenter
      text: backend.countdown
      color: theme.foreground
      font.pixelSize: Math.max(48, Math.min(win.width / 4, win.height / 3))
      font.bold: true
    }

    Row {
      anchors.horizontalCenter: parent.horizontalCenter
      spacing: 12
      AppButton { text: backend.isRunning ? "Pause" : "Start"; accent: true; onClicked: backend.toggle() }
      AppButton { text: "Reset"; onClicked: backend.reset() }
    }

    Label {
      anchors.horizontalCenter: parent.horizontalCenter
      text: "Space start/pause · Ctrl+R reset"
      color: Qt.alpha(theme.foreground, 0.6)
      font.pixelSize: 13
    }
  }

  Shortcut { sequence: "Space"; onActivated: backend.toggle() }
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
