import QtQuick
import Quickshell
import Quickshell.Io
import Quickshell.Wayland
import qs.Commons
import qs.Commons as Commons
import qs.Ui
import "plugin" as P

ShellRoot {
  id: root
  readonly property string position: ["top", "bottom", "left", "right"].indexOf(Quickshell.env("OMASHARP_BAR_POSITION")) >= 0 ? Quickshell.env("OMASHARP_BAR_POSITION") : "top"
  readonly property bool vertical: position === "left" || position === "right"
  readonly property string screenName: {
    try { return JSON.parse(Quickshell.env("OMASHARP_PANEL_PAYLOAD") || "{}").screen || "" } catch (e) { return "" }
  }

  FileView { id: manifest; path: decodeURIComponent(Qt.resolvedUrl("plugin/manifest.json").toString().replace("file://", "")); blockLoading: true }
  readonly property string pluginId: {
    try { return JSON.parse(manifest.text()).id || "" } catch (e) { return "" }
  }

  P.Service { id: service }

  PluginBarApi {
    id: api
    pluginId: root.pluginId
    moduleName: root.pluginId
    shell: QtObject { function serviceFor(id) { return id === root.pluginId ? service : null } }
    foreground: Commons.Color.bar.text
    barForeground: Commons.Color.bar.text
    background: Commons.Color.bar.background
    urgent: Commons.Color.bar.active
    fontFamily: Style.font.family
    position: root.position
    vertical: root.vertical
    barSize: root.vertical ? Style.bar.sizeVertical : Style.bar.sizeHorizontal
    _requestPopout: function(owner) { api.activePopout = owner }
    _releasePopout: function(owner) { if (api.activePopout === owner) api.activePopout = null }
    _registerClickTarget: function(target) { api.clickTargets = api.clickTargets.concat([target]) }
    _unregisterClickTarget: function(target) { api.clickTargets = api.clickTargets.filter(function(t) { return t !== target }) }
  }

  PanelWindow {
    id: bar
    screen: {
      for (var i = 0; i < Quickshell.screens.length; i++)
        if (Quickshell.screens[i].name === root.screenName) return Quickshell.screens[i]
      return null
    }
    anchors {
      top: root.position === "top" || root.vertical
      bottom: root.position === "bottom" || root.vertical
      left: root.position === "left" || !root.vertical
      right: root.position === "right" || !root.vertical
    }
    implicitWidth: root.vertical ? api.barSize : 0
    implicitHeight: root.vertical ? 0 : api.barSize
    color: api.background
    WlrLayershell.namespace: root.pluginId
    WlrLayershell.layer: WlrLayer.Top

    P.BarWidget {
      id: widget
      bar: api
      moduleName: root.pluginId
      x: root.vertical ? (parent.width - width) / 2 : parent.width - width - Style.space(8)
      y: root.vertical ? parent.height - height - Style.space(8) : (parent.height - height) / 2
      Component.onCompleted: open()
      onOpenedChanged: if (!opened) Qt.quit()
    }
  }

  TextDump { roots: [bar] }
}
