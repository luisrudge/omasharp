import QtQuick
import Quickshell
import Quickshell.Wayland
import qs.Commons
import qs.Commons as Commons

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

  function state() { return JSON.stringify({ ready: backend.ready, running: backend.running, count: backend.count, greeting: backend.greeting, runtime: backend.runtime, lastError: backend.lastError, lastCallError: root.lastCallError }) }
  function press(what) {
    if (what === "increment") backend.increment()
    else if (what === "slow") backend.countSlowly(3)
    else if (what === "fail") backend.fail().catch(function(e) { root.lastCallError = String(e) })
    else if (what === "crash") backend.crash()
    else if (what === "restart") backend.restart()
    else if (what.indexOf("name=") === 0) backend.name = what.slice(5)
    return "ok"
  }
  property string lastCallError: ""

  Backend {
    id: backend
    onExited: function(code) { console.warn("omasharp-hello: backend exited", code) }
  }

  PanelWindow {
    id: win
    visible: root.opened
    screen: {
      for (var i = 0; i < Quickshell.screens.length; i++)
        if (Quickshell.screens[i].name === root.screenName) return Quickshell.screens[i]
      return null
    }
    WlrLayershell.namespace: "omasharp-hello"
    WlrLayershell.layer: WlrLayer.Top
    WlrLayershell.keyboardFocus: WlrKeyboardFocus.None
    exclusionMode: ExclusionMode.Ignore
    anchors.top: true
    margins.top: 60
    implicitWidth: 420
    implicitHeight: 260
    color: "transparent"

    Rectangle {
      anchors.fill: parent
      color: Commons.Color.popups.background
      border.color: Commons.Color.popups.border
      border.width: 2
      radius: Style.cornerRadius

      Column {
        anchors.centerIn: parent
        spacing: 12
        Text { textFormat: Text.PlainText; anchors.horizontalCenter: parent.horizontalCenter; color: Commons.Color.popups.text; font.family: Style.font.family; font.pixelSize: 26; text: backend.ready ? backend.greeting : (backend.running ? "starting…" : "backend stopped") }
        Text { textFormat: Text.PlainText; anchors.horizontalCenter: parent.horizontalCenter; color: Commons.Color.muted; font.family: Style.font.family; font.pixelSize: 14; text: backend.time + " · " + backend.runtime }
        Text { textFormat: Text.PlainText; anchors.horizontalCenter: parent.horizontalCenter; color: Commons.Color.urgent; font.family: Style.font.family; font.pixelSize: 12; visible: !backend.running && backend.lastError !== ""; text: backend.lastError; width: 380; elide: Text.ElideRight; horizontalAlignment: Text.AlignHCenter }
        Row {
          anchors.horizontalCenter: parent.horizontalCenter
          spacing: 10
          Rectangle {
            width: 120; height: 36; color: ma.containsMouse ? Style.hoverFill : Style.normalFill; border.color: Style.normalBorderColor; radius: Style.cornerRadius
            Text { textFormat: Text.PlainText; anchors.centerIn: parent; color: Commons.Color.popups.text; font.family: Style.font.family; text: backend.running ? "+1" : "restart" }
            MouseArea { id: ma; anchors.fill: parent; hoverEnabled: true; onClicked: backend.running ? backend.increment() : backend.restart() }
          }
          Rectangle {
            width: 120; height: 36; color: ma2.containsMouse ? Style.hoverFill : Style.normalFill; border.color: Style.normalBorderColor; radius: Style.cornerRadius
            Text { textFormat: Text.PlainText; anchors.centerIn: parent; color: Commons.Color.popups.text; font.family: Style.font.family; text: "close" }
            MouseArea { id: ma2; anchors.fill: parent; hoverEnabled: true; onClicked: root.dismiss() }
          }
        }
      }
    }
  }
}
