// check.sh support (the Omasharp skill): with OMASHARP_TEXT_DUMP=<file>, write the visible texts
// under `roots` there, for both dev harnesses (dev-shell.qml, dev-bar.qml).
// Same output as the window version in bridge/host/textdump.cpp; keep them in step.
import QtQuick
import Quickshell
import Quickshell.Io

Scope {
  id: root
  property var roots: []

  readonly property string dumpPath: Quickshell.env("OMASHARP_TEXT_DUMP") || ""
  FileView { id: dump; path: root.dumpPath; blockWrites: true }
  Timer {
    interval: 250; repeat: true; running: root.dumpPath !== ""
    onTriggered: {
      var out = [], seen = []
      function kind(o) { return String(o).replace(/^QQuick/, "").replace(/\(0x[0-9a-f]+.*$/, "").replace(/_QML(TYPE)?_\d+$/, "") }
      function walk(o, depth) {
        if (!o || seen.indexOf(o) >= 0) return
        seen.push(o)
        if (o.visible === false || (o.opacity !== undefined && o.opacity <= 0.01)) return
        var masked = typeof o.echoMode === "number" && o.echoMode !== 0 // password fields: never the secret
        var text = (!masked && typeof o.text === "string" && o.text) || (typeof o.displayText === "string" && o.displayText) || ""
        if (!text && typeof o.placeholderText === "string" && o.placeholderText) text = "(placeholder) " + o.placeholderText
        if (text && kind(o) !== "PlaceholderText") out.push("  ".repeat(depth) + kind(o) + ": " + text.replace(/\s+/g, " ")
          + (o.checkable === true && typeof o.checked === "boolean" ? (o.checked ? " [checked]" : " [unchecked]") : "")
          + (o.activeFocus ? " [focus]" : ""))
        var d = text ? depth + 1 : depth
        if (o.contentItem) walk(o.contentItem, d)
        var kids = o.children || [], data = o.data || []
        for (var i = 0; i < kids.length; i++) walk(kids[i], d)
        for (var j = 0; j < data.length; j++) walk(data[j], d)
      }
      for (var r = 0; r < root.roots.length; r++) walk(root.roots[r], 0)
      dump.setText(out.join("\n") + "\n")
    }
  }
}
