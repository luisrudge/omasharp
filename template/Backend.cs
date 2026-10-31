using Omasharp;

public class Backend : BackendBase
{
    int _count;

    public int Count { get => _count; set { if (Set(ref _count, value)) Notify(nameof(Label)); } }

    public string Label => Count == 0 ? "Nothing counted yet" : $"Counted {Count} time{(Count == 1 ? "" : "s")}";

    public void Increment() => Count++;

    public void Reset() => Count = 0;
}
