using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Omasharp.Analyzers;

var failures = 0;

const string Bridge = """
    namespace Omasharp
    {
        public abstract class BackendBase : System.ComponentModel.INotifyPropertyChanged
        {
            public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
            protected bool Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null) { field = value; Notify(name!); return true; }
            protected void Notify(params string[] names) { foreach (var n in names) PropertyChanged?.Invoke(this, new(n)); }
            protected static void Raise(string name, object? value = null) { }
        }
    }
    """;

const string Usings = "using System; using System.Collections.Generic; using System.Linq; using System.Threading; using System.Threading.Tasks; using Omasharp;\n";

void Expect(string name, string source, string[] expected, string? backendType = null, string[]? suppressed = null)
{
    var (found, wasSuppressed, errors) = Analyze(Usings + source, backendType);
    var want = expected.OrderBy(x => x).ToArray();
    var ok = errors.Length == 0 && found.SequenceEqual(want) && (suppressed == null || suppressed.OrderBy(x => x).SequenceEqual(wasSuppressed));
    Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {name}");
    if (ok) return;
    failures++;
    foreach (var e in errors) Console.WriteLine($"     compile error: {e}");
    Console.WriteLine($"     expected [{string.Join(", ", want)}], got [{string.Join(", ", found)}]");
    if (suppressed != null) Console.WriteLine($"     CA1822 suppressed on [{string.Join(", ", wasSuppressed)}], expected [{string.Join(", ", suppressed.OrderBy(x => x))}]");
}

(string[] Found, string[] Suppressed, string[] Errors) Analyze(string source, string? backendType)
{
    var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Select(p => MetadataReference.CreateFromFile(p));
    var options = new CSharpParseOptions(LanguageVersion.Latest);
    var compilation = CSharpCompilation.Create("App",
        [CSharpSyntaxTree.ParseText(Bridge, options), CSharpSyntaxTree.ParseText(source, options)], refs,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToArray();
    var config = new Config(backendType == null ? [] : new() { ["build_property.BackendType"] = backendType });
    var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new BackendAnalyzer(), new FakeCA1822(), new StaticSuppressor());
    var withAnalyzers = compilation.WithAnalyzers(analyzers, new CompilationWithAnalyzersOptions(
        new AnalyzerOptions([], config), null, concurrentAnalysis: false, logAnalyzerExecutionTime: false, reportSuppressedDiagnostics: true));
    var all = withAnalyzers.GetAnalyzerDiagnosticsAsync().Result;
    var found = all.Where(d => !d.IsSuppressed && d.Id.StartsWith("OMASHARP", StringComparison.Ordinal)).Select(d => d.Id).OrderBy(x => x).ToArray();
    var suppressed = all.Where(d => d.Id == "CA1822" && d.IsSuppressed).Select(d => d.GetMessage()).OrderBy(x => x).ToArray();
    return (found, suppressed, errors);
}

Expect("the template's starter backend", """
    public class Backend : BackendBase
    {
        int _count;
        public int Count { get => _count; set { if (Set(ref _count, value)) Notify(nameof(Label)); } }
        public string Label => Count == 0 ? "Nothing counted yet" : $"Counted {Count} time{(Count == 1 ? "" : "s")}";
        public void Increment() => Count++;
        public void Reset() => Count = 0;
    }
    """, []);

Expect("hello: a timer, async, computed, pure members (CA1822 suppressed on the backend only)", """
    public sealed class Backend : BackendBase, IDisposable
    {
        int _count;
        string _name = "Omarchy";
        readonly Timer _clock;
        public Backend() { _clock = new Timer(_ => Notify(nameof(Time)), null, 1000, 1000); }
        public int Count { get => _count; set { if (Set(ref _count, value)) Notify(nameof(Greeting)); } }
        public string Name { get => _name; set { if (Set(ref _name, value)) Notify(nameof(Greeting)); } }
        public string Greeting => $"Hello, {Name}! ({Count})";
        public string Time => DateTime.Now.ToString("HH:mm:ss");
        public int Add(int a, int b) => a + b;
        public async Task CountSlowly(int times) { for (int i = 0; i < times; i++) { await Task.Delay(200); Count++; } }
        public void Dispose() => _clock.Dispose();
        int Helper() => 1;
    }
    public class Other { public int Pure() => 1; }
    """, [], suppressed: ["Add", "Time"]);

Expect("OMASHARP001: names QML can't take", """
    public class Backend : BackendBase
    {
        public void Delete() { }
        public bool Running => true;
        public void _Hidden() { }
        public string Keys => "";
    }
    public class NotTheBackend { public void Delete() { } }
    """, ["OMASHARP001", "OMASHARP001", "OMASHARP001", "OMASHARP001"]);

Expect("OMASHARP002: two members, one QML name", """
    public class Backend : BackendBase
    {
        public int Total => 1;
        public void total() { }
    }
    """, ["OMASHARP002"]);

Expect("OMASHARP003: public members QML can't see", """
    public class Backend : BackendBase
    {
        public int Field;
        public const int Max = 5;
        public static int Twice(int x) => x * 2;
        public static string Version => "1";
        public event EventHandler? Done;
        public string Secret { private get; set; } = "";
        public int this[int i] => i;
        public T Echo<T>(T x) => x;
        internal static int ForTests() => 1;
        public record Entry(string Name);
    }
    """, ["OMASHARP003", "OMASHARP003", "OMASHARP003", "OMASHARP003", "OMASHARP003", "OMASHARP003", "OMASHARP003", "OMASHARP003"]);

Expect("OMASHARP004: C# changes a property whose setter doesn't tell QML", """
    public class Backend : BackendBase
    {
        public string Status { get; private set; } = "";
        public void Load() => Status = "loaded";
    }
    """, ["OMASHARP004"]);

Expect("OMASHARP004 stays quiet: set at startup, set only by QML, notified elsewhere, Set() in the setter", """
    public class Backend : BackendBase
    {
        public double Scale { get; private set; }
        public Backend() { Scale = 2; }
        public string Query { get; set; } = "";
        public string Status { get; private set; } = "";
        public void Load() { Status = "loaded"; Notify(nameof(Status)); }
        string _mode = "";
        public string Mode { get => _mode; set => Set(ref _mode, value); }
        public void Dark() => Mode = "dark";
        public int Loaded { get; init; }
    }
    """, []);

Expect("OMASHARP005: computed from state C# changes, never notified", """
    public class Backend : BackendBase
    {
        readonly List<string> _items = new();
        public int ItemCount => _items.Count;
        public void AddItem(string s) => _items.Add(s);
        double _total;
        public string Summary { get { return $"{_total:F2}"; } }
        public void Bump() => _total++;
    }
    """, ["OMASHARP005", "OMASHARP005"]);

Expect("OMASHARP005 stays quiet: notified, set at startup, constant tables, QML-only inputs", """
    public class Backend : BackendBase
    {
        readonly List<string> _items = new();
        public int ItemCount => _items.Count;
        public void AddItem(string s) { _items.Add(s); Notify(nameof(ItemCount)); }
        static readonly string[] s_units = ["m", "km"];
        public int UnitCount => s_units.Length;
        readonly string _home;
        public Backend() { _home = "/home"; _scale = 2; }
        public string Home => _home + "/x";
        double _scale;
        public int Width => (int)(400 * _scale);
        public string Now => DateTime.Now.ToString();
        string _query = "";
        public string Query { get => _query; set { if (Set(ref _query, value)) Notify(nameof(Results)); } }
        public string Results => _query.ToUpperInvariant();
    }
    """, []);

Expect("OMASHARP005 follows other computed properties", """
    public class Backend : BackendBase
    {
        int _n;
        int Doubled => _n * 2;
        public string Shown => Doubled.ToString();
        public void Step() => _n++;
    }
    """, ["OMASHARP005"]);

Expect("OMASHARP006: Notify with a name that isn't a public property", """
    public class Backend : BackendBase
    {
        int _count;
        int Hidden => 1;
        public int Count { get => _count; set => Set(ref _count, value); }
        public void A() { Notify("Cuont"); }
        public void B() { Notify(nameof(Count), nameof(Hidden)); }
        public void C() { Notify(nameof(Count)); Notify(""); Raise("anything", 1); }
        public void D(string mode) { var m = mode; }
    }
    """, ["OMASHARP006", "OMASHARP006"]);

Expect("OMASHARP007: what QML can't call or set", """
    public record Item(string Name);
    public class Backend : BackendBase
    {
        public void Pick(Item item) { }
        public void Read(out int x) { x = 1; }
        public int Sum(params int[] xs) => xs.Sum();
        public void Show(int x) { }
        public void Show(string x) { }
        List<string> _names = new();
        public List<string> Names { get => _names; set => Set(ref _names, value); }
        public Task Fetch(string city, CancellationToken ct = default) => Task.CompletedTask;
        public void Fine(int a, double b, string? c, bool d, DayOfWeek e, string[] f, object g, long? h) { }
    }
    """, ["OMASHARP007", "OMASHARP007", "OMASHARP007", "OMASHARP007", "OMASHARP007", "OMASHARP007"]);

Expect("OMASHARP008: what QML can't use", """
    public record Weather(double Temp);
    public class Backend : BackendBase
    {
        public ValueTask Save() => default;
        public ValueTask<int> Load() => new(1);
        public (int X, int Y) Point() => (1, 2);
        public async IAsyncEnumerable<int> Stream() { await Task.Yield(); yield return 1; }
        public Task<(int, int)> Pair() => Task.FromResult((1, 2));
        public Task<Weather> Fetch() => Task.FromResult(new Weather(1));
        public List<Weather> Week => new();
    }
    """, ["OMASHARP008", "OMASHARP008", "OMASHARP008", "OMASHARP008", "OMASHARP008"]);

Expect("OMASHARP009 and OMASHARP010: async void, ConfigureAwait(false)", """
    public class Backend : BackendBase
    {
        public async void Go() { await Task.Delay(1); }
        async void Later() { await Task.Delay(1); }
        public async Task Fine() { await Task.Delay(1).ConfigureAwait(true); }
        public async Task Off() { await Task.Delay(1).ConfigureAwait(false); }
    }
    public class Elsewhere { public async Task Off() { await Task.Delay(1).ConfigureAwait(false); } }
    """, ["OMASHARP009", "OMASHARP009", "OMASHARP010"]);

Expect("BackendType: a class that implements INotifyPropertyChanged itself", """
    namespace Calc
    {
        using System.ComponentModel;
        using System.Runtime.CompilerServices;
        public class Backend : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler? PropertyChanged;
            string _display = "0";
            public string Display => _display;
            public double Scale { get; }
            public Backend() { Scale = 1; }
            public void Press(string key) { _display = key; Notify(nameof(Display)); Notify(); }
            public void Delete() { }
            public static double? Evaluate(string s) => null;
            internal static string Format(double d) => d.ToString();
            void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
        }
    }
    """, ["OMASHARP001", "OMASHARP003"], backendType: "Calc.Backend");

Expect("one member opts out: [SuppressMessage] and #pragma", """
    public class Backend : BackendBase
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Omasharp", "OMASHARP003", Justification = "only the tests call it")]
        public static int Parse(string s) => int.Parse(s);
    #pragma warning disable OMASHARP003 // the tests read it
        public const int Max = 5;
    #pragma warning restore OMASHARP003
        public const int Min = 0;
    }
    """, ["OMASHARP003"]);

Console.WriteLine(failures == 0 ? "all passed" : $"{failures} failed");
return failures == 0 ? 0 : 1;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
sealed class FakeCA1822 : DiagnosticAnalyzer
{
    static readonly DiagnosticDescriptor s_rule = new("CA1822", "static", "{0}", "Performance", DiagnosticSeverity.Warning, true);
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(c =>
        {
            var (id, name) = c.Node switch
            {
                MethodDeclarationSyntax m => (m.Identifier, m.Identifier.ValueText),
                PropertyDeclarationSyntax p => (p.Identifier, p.Identifier.ValueText),
                _ => default,
            };
            if (name is "Add" or "Time" or "Helper" or "Pure") c.ReportDiagnostic(Diagnostic.Create(s_rule, id.GetLocation(), name));
        }, SyntaxKind.MethodDeclaration, SyntaxKind.PropertyDeclaration);
    }
}

sealed class Config(Dictionary<string, string> global) : AnalyzerConfigOptionsProvider
{
    public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(global);
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new Options([]);
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new Options([]);

    sealed class Options(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value!);
    }
}
