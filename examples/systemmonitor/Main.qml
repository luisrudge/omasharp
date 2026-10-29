import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import Omasharp

ApplicationWindow {
  id: win
  width: 1100
  height: 780
  visible: true
  title: "System Monitor"
  color: theme.background

  font.family: "monospace"

  readonly property color dim: Qt.alpha(theme.foreground, 0.6)
  readonly property color well: Qt.alpha(theme.background, 0.6)
  readonly property bool compact: width < 900
  readonly property int gap: compact ? 12 : 20

  function level(fraction, high) {
    const f = high ? 1 - fraction : fraction
    return f > 0.9 ? theme.red : f > 0.75 ? theme.yellow : theme.green
  }

  function lines(text) { return compact ? text.split("  ·  ").join("\n") : text }

  Timer { interval: 250; running: true; onTriggered: backend.tick() }
  Timer { interval: 1000; running: true; repeat: true; onTriggered: backend.tick() }

  Flickable {
    id: flick
    anchors.fill: parent
    contentHeight: content.implicitHeight + 2 * content.y
    boundsBehavior: Flickable.StopAtBounds
    clip: true

    ColumnLayout {
      id: content
      x: win.compact ? 16 : 28; y: x
      width: parent.width - 2 * x
      height: Math.max(implicitHeight, flick.height - 2 * y)
      spacing: win.gap

      RowLayout {
        Layout.fillWidth: true
        spacing: 12
        Rectangle { width: 10; height: 10; radius: 5; color: theme.green; Layout.alignment: Qt.AlignVCenter
          SequentialAnimation on opacity { loops: Animation.Infinite
            NumberAnimation { to: 0.3; duration: 900; easing.type: Easing.InOutSine }
            NumberAnimation { to: 1; duration: 900; easing.type: Easing.InOutSine } }
        }
        Label { text: "System Monitor"; color: theme.foreground; font.pixelSize: win.compact ? 17 : 22; font.bold: true }
        Item { Layout.fillWidth: true }
        Label { text: backend.host; color: theme.accent; font.pixelSize: win.compact ? 12 : 14 }
        Label { text: "up " + backend.uptime; color: win.dim; font.pixelSize: win.compact ? 12 : 14 }
      }

      GridLayout {
        id: grid
        Layout.fillWidth: true
        Layout.fillHeight: true
        columns: width >= 560 ? 2 : 1
        columnSpacing: win.gap
        rowSpacing: win.gap
        readonly property real cell: (width - columnSpacing * (columns - 1)) / columns

        Card {
          Layout.preferredWidth: grid.cell
          title: "CPU"
          tint: theme.accent
          value: Math.round(backend.cpuPercent) + "%"
          caption: "load " + backend.loadAverage

          RowLayout {
            Layout.fillWidth: true
            Layout.fillHeight: true
            spacing: win.gap
            Graph { id: cpuGraph; source: "cpu"; Layout.preferredHeight: win.compact ? 120 : 170 }
            Column {
              spacing: 6
              Layout.alignment: Qt.AlignBottom
              Row {
                id: coreRow
                spacing: backend.cores.length > 8 ? 2 : 6
                readonly property real bar: Math.max(3, Math.min(win.compact ? 10 : 16, 100 / Math.max(1, backend.cores.length) - spacing))
                Repeater {
                  model: backend.cores
                  Rectangle {
                    required property var modelData
                    width: coreRow.bar; height: cpuGraph.height - 24
                    radius: 3
                    color: win.well
                    Rectangle {
                      anchors.bottom: parent.bottom
                      width: parent.width; radius: 3
                      height: Math.max(3, parent.height * modelData.percent / 100)
                      color: theme.accent
                      opacity: 0.45 + 0.55 * modelData.percent / 100
                      Behavior on height { NumberAnimation { duration: 500; easing.type: Easing.OutCubic } }
                    }
                  }
                }
              }
              Label {
                text: backend.cores.length + " cores"
                color: win.dim; font.pixelSize: 11
                anchors.horizontalCenter: parent.horizontalCenter
              }
            }
          }
          Footer { visible: !win.compact }
        }

        Card {
          Layout.preferredWidth: grid.cell
          title: "Thermals"
          tint: backend.hasTemp ? win.level(backend.cpuTemp / backend.tempLimit, false) : theme.green
          value: backend.hasTemp ? Math.round(backend.cpuTemp) + "°C" : "—"
          caption: backend.hasTemp ? backend.tempDetail : "No CPU temperature sensor"

          Graph { source: "temp"; visible: backend.hasTemp; Layout.preferredHeight: win.compact ? 56 : 90 }

          Repeater {
            model: backend.fans.length
            ColumnLayout {
              required property int index
              readonly property var modelData: backend.fans[index] ?? { label: "", rpm: "", fraction: -1 }
              Layout.fillWidth: true
              spacing: 6
              RowLayout {
                Layout.fillWidth: true
                spacing: 10
                Label { text: modelData.label; color: theme.foreground; font.pixelSize: win.compact ? 13 : 15; font.bold: true; elide: Text.ElideRight; Layout.fillWidth: true }
                Label { text: modelData.rpm; color: win.dim; font.pixelSize: win.compact ? 11 : 12 }
                Label {
                  visible: modelData.fraction >= 0
                  text: Math.round(modelData.fraction * 100) + "%"
                  color: theme.brightCyan; font.pixelSize: win.compact ? 13 : 15; font.bold: true
                  horizontalAlignment: Text.AlignRight
                  Layout.preferredWidth: 48
                }
              }
              Meter { visible: modelData.fraction >= 0; fraction: modelData.fraction; tint: theme.brightCyan }
            }
          }
          Label { visible: backend.fans.length === 0; text: "No fan sensors"; color: win.dim; font.pixelSize: 12 }
        }

        Card {
          Layout.preferredWidth: grid.cell
          title: "Memory"
          tint: theme.magenta
          value: Math.round(backend.memPercent) + "%"
          caption: backend.memUsed

          Graph { source: "memory" }
          Footer { text: backend.memDetail }
        }

        Card {
          Layout.preferredWidth: grid.cell
          title: "Network"
          tint: theme.brightCyan
          value: "↓ " + backend.down
          caption: "↑ " + backend.up
          captionColor: theme.yellow

          Graph { source: "network" }
          Footer { text: backend.netDetail }
        }

        Card {
          Layout.preferredWidth: grid.cell
          Layout.fillHeight: true
          title: "Storage"
          tint: theme.yellow
          caption: backend.disks.length + (backend.disks.length === 1 ? " volume" : " volumes")

          Repeater {
            model: backend.disks.length
            ColumnLayout {
              required property int index
              readonly property var modelData: backend.disks[index] ?? { mount: "", device: "", used: "", size: "", fraction: 0 }
              Layout.fillWidth: true
              Layout.topMargin: 4
              spacing: 8
              RowLayout {
                Layout.fillWidth: true
                spacing: 10
                Label { text: modelData.mount; color: theme.foreground; font.pixelSize: win.compact ? 13 : 15; font.bold: true }
                Label { text: modelData.device; color: win.dim; font.pixelSize: 12; elide: Text.ElideRight; Layout.fillWidth: true }
                Label { text: modelData.used + " of " + modelData.size; color: win.dim; font.pixelSize: win.compact ? 11 : 12 }
                Label {
                  text: Math.round(modelData.fraction * 100) + "%"
                  color: win.level(modelData.fraction, false); font.pixelSize: 15; font.bold: true
                  horizontalAlignment: Text.AlignRight
                  Layout.preferredWidth: 48
                }
              }
              Meter { fraction: modelData.fraction; tint: win.level(modelData.fraction, false) }
            }
          }
          Item { Layout.fillHeight: true }
        }

        Card {
          Layout.preferredWidth: grid.cell
          Layout.fillHeight: true
          title: "Battery"
          tint: backend.hasBattery ? win.level(backend.batteryPercent / 100, true) : theme.green
          value: backend.hasBattery ? Math.round(backend.batteryPercent) + "%" : "—"
          caption: backend.hasBattery ? (backend.charging ? "⚡ " : "") + backend.batteryStatus : "No battery"

          RowLayout {
            visible: backend.hasBattery
            Layout.fillWidth: true
            Layout.topMargin: 4
            spacing: win.gap

            Item {
              Layout.preferredWidth: shell.width + 8; Layout.preferredHeight: shell.height
              Rectangle {
                id: shell
                width: win.compact ? 84 : 124; height: win.compact ? 40 : 58; radius: win.compact ? 6 : 8
                color: "transparent"
                border.color: Qt.alpha(theme.foreground, 0.5); border.width: 2
                Rectangle {
                  id: charge
                  readonly property int inset: win.compact ? 4 : 6
                  x: inset; y: inset; height: parent.height - 2 * inset; radius: 4
                  width: Math.max(4, (parent.width - 2 * inset) * backend.batteryPercent / 100)
                  color: win.level(backend.batteryPercent / 100, true)
                  Behavior on width { NumberAnimation { duration: 600; easing.type: Easing.OutCubic } }
                  SequentialAnimation on opacity {
                    running: backend.batteryStatus === "Charging"; loops: Animation.Infinite
                    onRunningChanged: if (!running) charge.opacity = 1
                    NumberAnimation { to: 0.55; duration: 1100; easing.type: Easing.InOutSine }
                    NumberAnimation { to: 1; duration: 1100; easing.type: Easing.InOutSine }
                  }
                }
              }
              Rectangle { x: shell.width + 2; anchors.verticalCenter: shell.verticalCenter; width: 6; height: shell.height / 3; radius: 2; color: Qt.alpha(theme.foreground, 0.5) }
            }

            ColumnLayout {
              Layout.fillWidth: true
              spacing: 6
              Label { text: backend.batteryTime || (backend.charging || backend.batteryStatus === "Plugged in" ? "On AC power" : "On battery"); color: theme.foreground; font.pixelSize: win.compact ? 14 : 17; wrapMode: Text.Wrap; Layout.fillWidth: true }
              Label { text: win.lines(backend.batteryDetail); color: win.dim; font.pixelSize: 12; wrapMode: Text.Wrap; Layout.fillWidth: true }
            }
          }
          Item { Layout.fillHeight: true }
        }
      }
    }
  }

  Shortcut { sequence: "Ctrl+Q"; onActivated: win.close() }

  component Card: Rectangle {
    id: card
    property string title
    property color tint: theme.accent
    property string value
    property string caption
    property color captionColor: win.dim
    default property alias body: slot.data

    Layout.fillWidth: true
    Layout.fillHeight: true
    implicitHeight: slot.implicitHeight + 2 * slot.y
    color: theme.lighterBackground
    border.color: Qt.alpha(theme.muted, 0.6)
    radius: 6

    Rectangle { x: 1; y: 1; width: parent.width - 2; height: 2; radius: 1; color: card.tint; opacity: 0.8 }

    ColumnLayout {
      id: slot
      x: win.compact ? 14 : 20; y: x
      width: parent.width - 2 * x
      height: parent.height - 2 * y
      spacing: win.compact ? 10 : 14

      RowLayout {
        Layout.fillWidth: true
        spacing: 10
        ColumnLayout {
          Layout.fillWidth: true
          spacing: 4
          Label { text: card.title.toUpperCase(); color: card.tint; font.pixelSize: 12; font.bold: true; font.letterSpacing: 2 }
          Label { text: card.caption; visible: text !== ""; color: card.captionColor; font.pixelSize: 12; elide: Text.ElideRight; Layout.fillWidth: true }
        }
        Label { text: card.value; color: card.tint; font.pixelSize: win.compact ? 20 : 30; font.bold: true }
      }
    }
  }

  component Graph: Rectangle {
    property alias source: view.source
    Layout.fillWidth: true
    Layout.fillHeight: true
    Layout.preferredHeight: win.compact ? 104 : 150
    color: win.well
    radius: 4
    clip: true
    DrawView {
      id: view
      anchors.fill: parent
      function report() { if (width > 0 && height > 0) backend.resize(source, width, height) }
      onWidthChanged: report()
      onHeightChanged: report()
      Component.onCompleted: report()
    }
  }

  component Footer: RowLayout {
    property string text
    Layout.fillWidth: true
    Label { id: detail; text: win.lines(parent.text); color: win.dim; font.pixelSize: win.compact ? 11 : 12; wrapMode: Text.Wrap; Layout.fillWidth: true }
    Label { text: "last 2 min"; visible: !win.compact; color: Qt.alpha(theme.foreground, 0.4); font.pixelSize: 11; Layout.alignment: Qt.AlignBottom }
  }

  component Meter: Rectangle {
    property real fraction
    property color tint
    Layout.fillWidth: true
    implicitHeight: 8
    radius: 4
    color: win.well
    Rectangle {
      width: Math.max(height, parent.width * Math.min(1, parent.fraction))
      height: parent.height; radius: 4
      color: parent.tint
      Behavior on width { NumberAnimation { duration: 600; easing.type: Easing.OutCubic } }
    }
  }
}
