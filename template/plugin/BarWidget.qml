import QtQuick
import Quickshell
import qs.Commons
import qs.Commons as Commons
import qs.Ui

Panel {
  id: root
  implicitWidth: button.implicitWidth
  implicitHeight: button.implicitHeight

  readonly property QtObject backend: {
    var service = bar && bar.shell ? bar.shell.serviceFor(bar.pluginId) : null
    return service ? service.backend : idle
  }
  readonly property bool connected: backend !== idle
  Backend { id: idle; active: false }
  function countOrRestart() { if (backend.running) backend.increment(); else if (connected) backend.restart() }

  BarIconButton {
    id: button
    anchors.fill: parent
    bar: root.bar
    text: String(backend.count)
    tooltipText: backend.label
    active: root.opened
    onPressed: function(b) { if (b === Qt.RightButton) root.countOrRestart(); else root.toggle() }
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

    PanelKeyCatcher {
      id: keys
      anchors.fill: parent
      onCloseRequested: root.close()
      onActivateRequested: root.countOrRestart()
      onTextKey: function(t) { if (t === "r" && backend.running) backend.reset() }
      onTabRequested: function(direction) { root.switchPanel(direction) }

      Column {
        id: column
        width: parent.width
        spacing: Style.space(16)

        Text {
          width: parent.width
          horizontalAlignment: Text.AlignHCenter
          wrapMode: Text.Wrap
          textFormat: Text.PlainText
          text: backend.ready ? backend.label : (backend.running || !root.connected ? "Starting…" : "Backend stopped: " + backend.lastError)
          color: Commons.Color.popups.text
          font.family: Style.font.family
          font.pixelSize: Style.font.heading
        }

        Row {
          anchors.horizontalCenter: parent.horizontalCenter
          spacing: Style.space(12)
          Button { text: root.connected && !backend.running ? "Restart" : "Count"; bordered: true; onClicked: root.countOrRestart() }
          Button { text: "Reset"; bordered: true; enabled: backend.running; opacity: enabled ? 1 : 0.5; onClicked: backend.reset() }
        }
      }
    }
  }
}
