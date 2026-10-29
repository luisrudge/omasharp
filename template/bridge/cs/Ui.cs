using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Omasharp;

public abstract class BackendBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Notify(name!);
        return true;
    }

    protected void Notify(params string[] names)
    {
        foreach (var name in names) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    protected static void Raise(string name, object? value = null) => Bridge.Raise(name, value);
}

public static class Bridge
{
    public static bool IsNativeAot { get; } = DetectAot();

    public static string[] Args { get; internal set; } = [];

    internal static Action<string, string>? RaiseSink { get; set; }
    internal static Action<string, byte[]>? DrawSink { get; set; }

    public static void Show(string name, Drawing drawing)
    {
        var bytes = drawing.ToBytes();
        if (Ui.IsUiThread) DrawSink?.Invoke(name, bytes);
        else Ui.Post(() => DrawSink?.Invoke(name, bytes));
    }

    public static void Raise(string name, object? value = null)
    {
        var json = Json.Encode(value);
        Ui.Post(() => RaiseSink?.Invoke(name, json));
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("SingleFile", "IL3000",
        Justification = "Location is empty under native AOT; that's what we test for.")]
    static bool DetectAot() => string.IsNullOrEmpty(typeof(object).Assembly.Location);
}

public static class Ui
{
    static Action<Action>? s_post;
    static int s_threadId;

    public static bool IsUiThread => s_post == null || Environment.CurrentManagedThreadId == s_threadId;

    public static void Post(Action action)
    {
        if (s_post != null) s_post(action);
        else action();
    }

    internal static void Init(Action<Action> post)
    {
        s_post = post;
        s_threadId = Environment.CurrentManagedThreadId;
        SynchronizationContext.SetSynchronizationContext(new UiContext());
    }

    internal static void Run(Action action)
    {
        try { action(); }
        catch (Exception e) { Log.Error("posted action", e); }
    }

    sealed class UiContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => Ui.Post(() => d(state));
        public override void Send(SendOrPostCallback d, object? state)
        {
            if (IsUiThread) { d(state); return; }
            using var done = new ManualResetEventSlim();
            Ui.Post(() => { try { d(state); } finally { done.Set(); } });
            done.Wait();
        }
        public override SynchronizationContext CreateCopy() => this;
    }
}
