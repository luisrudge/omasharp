import QtQuick
import Quickshell
import qs.Commons
import qs.Commons as Commons // Qt 6.12 has its own Color type: the shell palette is Commons.Color
import qs.Ui

// The icon in the bar and its dropdown. `backend` is Backend.cs (running as a child process; a crash
// there can't take the shell down). Colors and sizes come from the shell's theme: Commons.Color.popups.background/.text,
// Commons.Color.accent, Style.font.body/.heading, ... and the shell's own controls from qs.Ui (see .agents/skills/omasharp/references/look.md).
// Like Omarchy's own bar widgets: a click opens the dropdown; Escape, a click outside or a second click closes it.
Panel {
  id: root
  implicitWidth: button.implicitWidth
  implicitHeight: button.implicitHeight

  // The backend runs once in Service.qml, for every monitor's bar. `idle` stands in until the shell hands it over;
  // it never runs, so restart the backend only when `connected`.
  readonly property QtObject backend: {
    var service = bar && bar.shell ? bar.shell.serviceFor(bar.pluginId) : null
    return service ? service.backend : idle
  }
  readonly property bool connected: backend !== idle
  Backend { id: idle; active: false }
  function countOrRestart() { if (backend.running) backend.increment(); else if (connected) backend.restart() }

  // The icon: a Nerd Font glyph or short text. Left click opens the dropdown, right click counts.
  BarIconButton {
    id: button
    anchors.fill: parent
    bar: root.bar
    text: String(backend.count)
    tooltipText: backend.label
    active: root.opened
    onPressed: function(b) { if (b === Qt.RightButton) backend.increment(); else root.toggle() }
  }

  KeyboardPanel {
    id: panel
    anchorItem: button
    owner: root
    bar: root.bar
    open: root.opened
    focusTarget: keys
    contentWidth: panel.fittedContentWidth(Style.space(280))
    contentHeight: panel.fittedContentHeight(column.implicitHeight)

    // Keys for the starter's actions: Return or Space counts, R resets, Escape closes, Tab moves to the next bar panel.
    PanelKeyCatcher {
      id: keys
      anchors.fill: parent
      onCloseRequested: root.close()
      onActivateRequested: root.countOrRestart()
      onTextKey: function(t) { if (t === "r") backend.reset() }
      onTabRequested: function(direction) { root.switchPanel(direction) }

      Column {
        id: column
        width: parent.width
        spacing: Style.space(16)

        Text {
          width: parent.width
          horizontalAlignment: Text.AlignHCenter
          wrapMode: Text.Wrap
          text: backend.ready ? backend.label : (backend.running || !root.connected ? "Starting…" : "Backend stopped: " + backend.lastError)
          color: Commons.Color.popups.text
          font.family: Style.font.family
          font.pixelSize: Style.font.heading
        }

        Row {
          anchors.horizontalCenter: parent.horizontalCenter
          spacing: Style.space(12)
          Button { text: root.connected && !backend.running ? "Restart" : "Count"; bordered: true; onClicked: root.countOrRestart() }
          Button { text: "Reset"; bordered: true; onClicked: backend.reset() }
        }
      }
    }
  }
}
