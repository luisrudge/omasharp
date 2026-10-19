import QtQuick

Window {
  width: 420; height: 260
  visible: true
  title: "Omasharp hello"
  color: "#1a1b26"

  Column {
    anchors.centerIn: parent; spacing: 12
    Text { color: "#a9b1d6"; font.family: "monospace"; font.pixelSize: 26; text: backend.greeting }
    Text { color: "#565f89"; font.family: "monospace"; font.pixelSize: 14; text: backend.time + " · " + backend.runtime }
    Text { color: "#565f89"; font.family: "monospace"; font.pixelSize: 14; text: "2 + 3 = " + backend.add(2, 3) }
  }

  // Test hooks: OMASHARP_SELFTEST=1 exercises the bridge from QML and prints the results.
  Timer {
    interval: 300; running: Qt.application.arguments.indexOf("--selftest") >= 0
    onTriggered: {
      var out = []
      out.push("count=" + backend.count + " greeting=" + backend.greeting)
      backend.increment(); backend.increment()
      out.push("after 2x increment: count=" + backend.count + " greeting=" + backend.greeting)
      backend.name = "World"
      out.push("after name=World: greeting=" + backend.greeting)
      backend.greeting = "nope"
      out.push("after writing read-only greeting: " + backend.greeting)
      try { backend.fail(); out.push("fail() did not throw") } catch (e) { out.push("fail() threw: " + e.message) }
      out.push("add(2,3)=" + backend.add(2, 3) + " runtime=" + backend.runtime)
      backend.countSlowly(2)
      console.log(out.join("\n"))
      later.start()
    }
  }
  Timer { id: later; interval: 1300; onTriggered: { console.log("after countSlowly(2): count=" + backend.count + " time=" + backend.time); Qt.quit() } }
}
