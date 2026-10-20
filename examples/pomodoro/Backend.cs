// The app's logic. QML sees this object as `backend`: public properties and methods,
// camelCased (Countdown -> backend.countdown, Toggle() -> backend.toggle()).
// See the omasharp skill (.agents/skills/omasharp/).
using System.Diagnostics;
using Omasharp;

public class Backend : BackendBase
{
    internal static readonly TimeSpan FocusLength = TimeSpan.FromMinutes(25);
    internal static readonly TimeSpan BreakLength = TimeSpan.FromMinutes(5);

    readonly Func<DateTime> _now;
    bool _onBreak;
    bool _running;
    TimeSpan _left = FocusLength; // time left while paused
    DateTime _endsAt;             // when the session ends while running

    // Called when a session ends; replaced in tests.
    internal Action<string, string> Alert { get; set; } = SendNotification;

    public Backend() : this(() => DateTime.UtcNow) { }

    internal Backend(Func<DateTime> now) => _now = now;

    public bool IsRunning { get => _running; private set => Set(ref _running, value); }

    public bool OnBreak { get => _onBreak; private set { if (Set(ref _onBreak, value)) Notify(nameof(Phase)); } }

    public string Phase => OnBreak ? "Break" : "Focus";

    public string Countdown
    {
        get
        {
            var left = IsRunning ? _endsAt - _now() : _left;
            var seconds = Math.Max(0, (int)Math.Ceiling(left.TotalSeconds));
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }
    }

    public void Toggle()
    {
        if (IsRunning) _left = _endsAt - _now();
        else _endsAt = _now() + _left;
        IsRunning = !IsRunning;
        Notify(nameof(Countdown));
    }

    public void Reset()
    {
        IsRunning = false;
        OnBreak = false;
        _left = FocusLength;
        Notify(nameof(Countdown));
    }

    // QML calls this a few times a second while running.
    public void Tick()
    {
        if (!IsRunning) return;
        if (_now() >= _endsAt)
        {
            // The next session starts right away, counted from when this one ended.
            OnBreak = !OnBreak;
            _endsAt += OnBreak ? BreakLength : FocusLength;
            if (OnBreak) Alert("Focus session done", "Take a 5-minute break.");
            else Alert("Break's over", "Back to focus for 25 minutes.");
        }
        Notify(nameof(Countdown));
    }

    static void SendNotification(string title, string body)
    {
        try { Process.Start("notify-send", ["-a", "Pomodoro", title, body])?.Dispose(); }
        catch (System.ComponentModel.Win32Exception) { } // notify-send missing: the window still shows it
    }
}
