#if OMASHARP_STDIO
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Omasharp;

static class StdioHost
{
    static StreamWriter s_out = null!;

    static int Main(string[] args)
    {
        if (args is ["--qml-shim", var path])
        {
            var instance = new AppBackend();
            try { File.WriteAllText(path, Shim.Generate(new Model(typeof(AppBackend), instance))); }
            catch (BridgeException e) { Console.Error.WriteLine($"Backend.cs : error OMASHARP001 : {e.Message}"); return 1; }
            finally { (instance as IDisposable)?.Dispose(); }
            return 0;
        }

        s_out = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
        Console.SetOut(Console.Error);

        var queue = new BlockingCollection<Action>();
        Ui.Init(a => { try { queue.Add(a); } catch (InvalidOperationException) { } });

        Bridge.Args = args;
        Bridge.RaiseSink = (name, value) => Send($"{{\"op\":\"raised\",\"name\":{Json.Encode(name)},\"value\":{value}}}");
        var backend = new AppBackend();
        var model = new Model(typeof(AppBackend), backend);
        model.Changed += Notify;
        var hello = model.Describe();
        Send("{\"op\":\"hello\"," + hello[1..]);

        new Thread(() =>
        {
            var stdin = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
            string? line;
            while ((line = stdin.ReadLine()) != null)
            {
                var l = line;
                queue.Add(() => Handle(model, l));
            }
            queue.CompleteAdding();
        }) { IsBackground = true, Name = "stdin" }.Start();

        foreach (var action in queue.GetConsumingEnumerable()) Ui.Run(action);
        try { (backend as IDisposable)?.Dispose(); }
        catch (Exception e) { Log.Error("Dispose", e); }
        Environment.Exit(0);
        return 0;
    }

    static void Send(string json) => s_out.WriteLine(json);

    static void Notify(string name, string value) =>
        Send($"{{\"op\":\"notify\",\"name\":{Json.Encode(name)},\"value\":{value}}}");

    static void Handle(Model model, string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        JsonElement id = default;
        string? name = null;
        try
        {
            using var doc = JsonDocument.Parse(line);
            var msg = doc.RootElement;
            if (msg.TryGetProperty("id", out var i)) id = i.Clone();
            name = msg.TryGetProperty("name", out var n) ? n.GetString() : null;
            var op = msg.GetProperty("op").GetString();
            switch (op)
            {
                case "get":
                    Reply(id, model.Get(name!));
                    break;
                case "set":
                    Notify(name!, model.Set(name!, msg.GetProperty("value").GetRawText()));
                    break;
                case "call":
                    var args = msg.TryGetProperty("args", out var a) ? a.GetRawText() : "[]";
                    var value = model.Call(name!, args, out var pending);
                    if (pending == null) { Reply(id, value ?? "null"); break; }
                    var callId = id; var callName = name;
                    pending.ContinueWith(t => Ui.Post(() =>
                    {
                        try { Reply(callId, Model.ResultJson(t)); }
                        catch (Exception e)
                        {
                            Console.Error.WriteLine($"omasharp: {callName}: {e}");
                            var idJson = callId.ValueKind == JsonValueKind.Undefined ? "null" : callId.GetRawText();
                            Send($"{{\"op\":\"error\",\"id\":{idJson},\"name\":{Json.Encode(callName)},\"message\":{Json.Encode(e.Message)}}}");
                        }
                    }), TaskScheduler.Default);
                    break;
                default:
                    throw new BridgeException($"unknown op '{op}'");
            }
        }
        catch (Exception e)
        {
            var err = e is BridgeException ? e.Message : e.ToString();
            Console.Error.WriteLine($"omasharp: {name}: {err}");
            var idJson = id.ValueKind == JsonValueKind.Undefined ? "null" : id.GetRawText();
            Send($"{{\"op\":\"error\",\"id\":{idJson},\"name\":{Json.Encode(name)},\"message\":{Json.Encode(e.Message)}}}");
            if (name != null && model.PropertyNames.Contains(name)) Notify(name, model.Get(name));
        }
    }

    static void Reply(JsonElement id, string value)
    {
        if (id.ValueKind == JsonValueKind.Undefined) return;
        Send($"{{\"op\":\"result\",\"id\":{id.GetRawText()},\"value\":{value}}}");
    }
}
#endif
