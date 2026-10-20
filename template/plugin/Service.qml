import QtQuick

// Started by omarchy-shell once per session while the widget is in the bar, and shared by every
// monitor's bar, so each icon shows the same state. See .agents/skills/omasharp/references/bar-widgets.md.
QtObject {
  readonly property QtObject backend: Backend { }
}
