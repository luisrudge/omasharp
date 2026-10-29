using Omasharp;

public sealed class Backend : BackendBase, IDisposable
{
    int _count;
    string _name = "Omarchy";
    readonly Timer _clock;

    public Backend()
    {
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
            Count++;
        }
    }

    public void Fail() => throw new InvalidOperationException("Fail() was called");

    public void Crash() => new Thread(() => throw new InvalidOperationException("crash test")).Start();

    public void Dispose() => _clock.Dispose();
}
