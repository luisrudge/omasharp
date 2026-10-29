global using AppBackend = Backend;

var failures = 0;
void Check(string name, bool ok)
{
    Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {name}");
    if (!ok) failures++;
}

var now = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
var alerts = new List<string>();
var b = new Backend(() => now) { Alert = (title, _) => alerts.Add(title) };

Check("starts paused on a 25-minute focus", !b.IsRunning && b.Phase == "Focus" && b.Countdown == "25:00");

b.Toggle();
now = now.AddSeconds(0.4);
b.Tick();
Check("counts down, rounding up", b.IsRunning && b.Countdown == "25:00");
now = now.AddSeconds(61);
b.Tick();
Check("one minute in", b.Countdown == "23:59");

b.Toggle();
now = now.AddMinutes(10);
b.Tick();
Check("pausing stops the clock", !b.IsRunning && b.Countdown == "23:59");

b.Toggle();
now = now.AddSeconds(23 * 60 + 59);
b.Tick();
Check("focus ends into a 5-minute break", b.Phase == "Break" && b.IsRunning && b.Countdown == "05:00" && alerts.SequenceEqual(["Focus session done"]));

now = now.AddMinutes(5);
b.Tick();
Check("break ends into focus", b.Phase == "Focus" && b.Countdown == "25:00" && alerts.Count == 2);

b.Reset();
Check("reset goes back to a paused focus", !b.IsRunning && b.Phase == "Focus" && b.Countdown == "25:00");

Console.WriteLine(failures == 0 ? "all passed" : $"{failures} failed");
return failures == 0 ? 0 : 1;
