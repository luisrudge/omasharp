// The app's logic. The same file runs in a window app (in-process, native AOT
// .so) and in an Omarchy shell panel (child process over JSON lines).
using Omasharp;

public sealed class Backend : BackendBase, IDisposable
{
    int _count;
    string _name = "Omarchy";
    readonly Timer _clock;

    public Backend()
    {
        // Fires on a thread-pool thread; the bridge moves the change to the UI thread.
        _clock = new Timer(_ => Notify(nameof(Time)), null, 1000, 1000);
    }

    public int Count { get => _count; set { if (Set(ref _count, value)) Notify(nameof(Greeting)); } }
    public string Name { get => _name; set { if (Set(ref _name, value)) Notify(nameof(Greeting)); } }
    public string Greeting => $"Hello, {Name}! ({Count})";
    public string Time => DateTime.Now.ToString("HH:mm:ss");
    public string Runtime => Omasharp.Bridge.IsNativeAot ? "native AOT" : ".NET runtime (JIT)";

    public void Increment() => Count++;
    public int Add(int a, int b) => a + b;

    public async Task CountSlowly(int times)
    {
        for (int i = 0; i < times; i++)
        {
            await Task.Delay(200);
            Count++; // back on the UI thread after await
        }
    }

    public void Fail() => throw new InvalidOperationException("Fail() was called");

    // Crash test: an unhandled exception on a background thread ends the process.
    public void Crash() => new Thread(() => throw new InvalidOperationException("crash test")).Start();

    // The bridge calls this when the app exits (window closed, or the shell closed the panel).
    public void Dispose() => _clock.Dispose();
}
