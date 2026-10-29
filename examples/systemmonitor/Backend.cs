using System.Diagnostics;
using System.Globalization;
using Omasharp;

public sealed record CoreLoad(string Label, double Percent);
public sealed record DiskUsage(string Mount, string Device, string Used, string Size, double Fraction);
public sealed record FanSpeed(string Label, string Rpm, double Fraction);

public sealed class Backend : BackendBase
{
    internal const int Samples = 120;

    readonly Queue<double> _cpuHistory = new(), _memHistory = new(), _downHistory = new(), _upHistory = new(), _tempHistory = new();
    readonly Dictionary<string, (double W, double H)> _sizes = [];
    readonly Stopwatch _clock = Stopwatch.StartNew();
    List<Probe.CpuTimes>? _cpuBefore;
    (double Rx, double Tx)? _netBefore;
    double _netAt;

    double _cpuPercent, _memPercent, _batteryPercent, _cpuTemp, _tempLimit = 100;
    string _uptime = "", _loadAverage = "", _memUsed = "", _memDetail = "", _down = "", _up = "", _netDetail = "",
        _batteryStatus = "", _batteryTime = "", _batteryDetail = "", _tempDetail = "";
    bool _hasBattery, _charging, _hasTemp;
    List<FanSpeed> _fans = [];
    List<CoreLoad> _cores = [];
    List<DiskUsage> _disks = [];

    public Backend()
    {
        Theme.Changed += Redraw;
        Tick();
    }

    public string Host { get; } = Environment.MachineName.ToLowerInvariant();
    public string Uptime { get => _uptime; private set => Set(ref _uptime, value); }

    public double CpuPercent { get => _cpuPercent; private set => Set(ref _cpuPercent, value); }
    public string LoadAverage { get => _loadAverage; private set => Set(ref _loadAverage, value); }
    public List<CoreLoad> Cores { get => _cores; private set => Set(ref _cores, value); }

    public double MemPercent { get => _memPercent; private set => Set(ref _memPercent, value); }
    public string MemUsed { get => _memUsed; private set => Set(ref _memUsed, value); }
    public string MemDetail { get => _memDetail; private set => Set(ref _memDetail, value); }

    public string Down { get => _down; private set => Set(ref _down, value); }
    public string Up { get => _up; private set => Set(ref _up, value); }
    public string NetDetail { get => _netDetail; private set => Set(ref _netDetail, value); }

    public bool HasTemp { get => _hasTemp; private set => Set(ref _hasTemp, value); }
    public double CpuTemp { get => _cpuTemp; private set => Set(ref _cpuTemp, value); }
    public double TempLimit { get => _tempLimit; private set => Set(ref _tempLimit, value); }
    public string TempDetail { get => _tempDetail; private set => Set(ref _tempDetail, value); }
    public List<FanSpeed> Fans { get => _fans; private set => Set(ref _fans, value); }

    public List<DiskUsage> Disks { get => _disks; private set => Set(ref _disks, value); }

    public bool HasBattery { get => _hasBattery; private set => Set(ref _hasBattery, value); }
    public bool Charging { get => _charging; private set => Set(ref _charging, value); }
    public double BatteryPercent { get => _batteryPercent; private set => Set(ref _batteryPercent, value); }
    public string BatteryStatus { get => _batteryStatus; private set => Set(ref _batteryStatus, value); }
    public string BatteryTime { get => _batteryTime; private set => Set(ref _batteryTime, value); }
    public string BatteryDetail { get => _batteryDetail; private set => Set(ref _batteryDetail, value); }

    public void Resize(string graph, double width, double height)
    {
        _sizes[graph] = (width, height);
        Redraw();
    }

    public void Tick()
    {
        SampleCpu();
        SampleMemory();
        SampleNetwork();
        SampleSensors();
        SampleDisks();
        SampleBattery();
        var uptime = File.ReadAllText("/proc/uptime").Split(' ')[0];
        Uptime = Probe.Uptime(double.Parse(uptime, CultureInfo.InvariantCulture));
        Redraw();
    }

    internal IReadOnlyCollection<double> CpuHistory => _cpuHistory;
    internal IReadOnlyCollection<double> MemHistory => _memHistory;
    internal IReadOnlyCollection<double> DownHistory => _downHistory;
    internal IReadOnlyCollection<double> TempHistory => _tempHistory;

    static void Push(Queue<double> history, double value)
    {
        history.Enqueue(value);
        while (history.Count > Samples) history.Dequeue();
    }

    void SampleCpu()
    {
        var now = Probe.ParseStat(File.ReadAllText("/proc/stat"));
        var before = _cpuBefore;
        _cpuBefore = now;
        var load = File.ReadAllText("/proc/loadavg").Split(' ');
        LoadAverage = $"{load[0]}  {load[1]}  {load[2]}";
        if (before is null || before.Count != now.Count) return;
        CpuPercent = Probe.Busy(before[0], now[0]);
        Push(_cpuHistory, CpuPercent);
        Cores = [.. now.Skip(1).Select((t, i) => new CoreLoad((i + 1).ToString(CultureInfo.InvariantCulture), Probe.Busy(before[i + 1], t)))];
    }

    void SampleMemory()
    {
        var m = Probe.ParseMeminfo(File.ReadAllText("/proc/meminfo"));
        double total = m.GetValueOrDefault("MemTotal"), used = total - m.GetValueOrDefault("MemAvailable");
        double swapTotal = m.GetValueOrDefault("SwapTotal"), swapUsed = swapTotal - m.GetValueOrDefault("SwapFree");
        MemPercent = total > 0 ? 100 * used / total : 0;
        Push(_memHistory, MemPercent);
        MemUsed = $"{Probe.Bytes(used)} of {Probe.Bytes(total)}";
        var cache = m.GetValueOrDefault("Cached") + m.GetValueOrDefault("Buffers");
        MemDetail = $"cache {Probe.Bytes(cache)}" + (swapTotal > 0 ? $"  ·  swap {Probe.Bytes(swapUsed)} of {Probe.Bytes(swapTotal)}" : "");
    }

    void SampleNetwork()
    {
        var all = Probe.ParseNetDev(File.ReadAllText("/proc/net/dev"));
        var physical = all.Where(kv => Directory.Exists($"/sys/class/net/{kv.Key}/device")).ToList();
        double rx = physical.Sum(kv => kv.Value.Rx), tx = physical.Sum(kv => kv.Value.Tx);
        var at = _clock.Elapsed.TotalSeconds;
        if (_netBefore is { } b && at > _netAt)
        {
            double down = Math.Max(0, rx - b.Rx) / (at - _netAt), up = Math.Max(0, tx - b.Tx) / (at - _netAt);
            Push(_downHistory, down);
            Push(_upHistory, up);
            Down = Probe.Rate(down);
            Up = Probe.Rate(up);
        }
        _netBefore = (rx, tx);
        _netAt = at;
        var active = physical.Where(kv => ReadOr($"/sys/class/net/{kv.Key}/operstate", "") == "up").Select(kv => kv.Key);
        NetDetail = $"{string.Join(", ", active.DefaultIfEmpty("offline"))}  ·  {Probe.Bytes(rx)} in, {Probe.Bytes(tx)} out since boot";
    }

    void SampleSensors()
    {
        var temps = new List<Probe.Sensor>();
        var fans = new List<Probe.Sensor>();
        var dirs = Directory.Exists("/sys/class/hwmon") ? Directory.GetDirectories("/sys/class/hwmon") : [];
        foreach (var dir in dirs.Order(StringComparer.Ordinal))
        {
            var chip = ReadOr(Path.Combine(dir, "name"), "");
            var cpuChip = chip is "coretemp" or "k10temp" or "zenpower" or "cpu_thermal";
            foreach (var input in Directory.GetFiles(dir, "*_input").Order(StringComparer.Ordinal))
            {
                var name = Path.GetFileName(input);
                var isFan = name.StartsWith("fan", StringComparison.Ordinal);
                if (!isFan && !(cpuChip && name.StartsWith("temp", StringComparison.Ordinal))) continue;
                var stem = input[..^"_input".Length];
                double? Num(string suffix) => double.TryParse(ReadOr(stem + suffix, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
                if (Num("_input") is not { } value) continue;
                var label = ReadOr(stem + "_label", "");
                if (isFan) fans.Add(new Probe.Sensor(chip, label == "" ? $"Fan {fans.Count + 1}" : label, value, Num("_min"), Num("_max")));
                else temps.Add(new Probe.Sensor(chip, label, value / 1000, Crit: (Num("_crit") ?? Num("_max")) / 1000));
            }
        }

        var cpu = Probe.PickCpuTemp(temps);
        HasTemp = cpu is not null;
        if (cpu is not null)
        {
            CpuTemp = cpu.Value;
            TempLimit = cpu.Crit is > 40 and { } crit ? crit : 100;
            Push(_tempHistory, CpuTemp);
            TempDetail = $"CPU  ·  limit {TempLimit:0}°C";
        }
        var speeds = fans.Select(f => new FanSpeed(f.Label, $"{f.Value:0} rpm", Probe.FanFraction(f))).ToList();
        if (!speeds.SequenceEqual(Fans)) Fans = speeds;
    }

    void SampleDisks()
    {
        var disks = new List<DiskUsage>();
        foreach (var (device, mount) in Probe.ParseMounts(File.ReadAllText("/proc/mounts")))
        {
            try
            {
                var drive = new DriveInfo(mount);
                double used = drive.TotalSize - drive.TotalFreeSpace, size = used + drive.AvailableFreeSpace;
                if (drive.TotalSize <= 0) continue;
                disks.Add(new DiskUsage(mount, device.Replace("/dev/mapper/", "", StringComparison.Ordinal).Replace("/dev/", "", StringComparison.Ordinal),
                    Probe.Bytes(used), Probe.Bytes(drive.TotalSize), size > 0 ? used / size : 0));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        if (!disks.SequenceEqual(Disks)) Disks = disks;
    }

    void SampleBattery()
    {
        var path = Directory.Exists("/sys/class/power_supply")
            ? Directory.GetDirectories("/sys/class/power_supply").Select(d => Path.Combine(d, "uevent")).FirstOrDefault(f => Probe.ParseBattery(ReadOr(f, "")) is not null)
            : null;
        var info = path is null ? null : Probe.ParseBattery(ReadOr(path, ""));
        HasBattery = info is not null;
        if (info is null) return;
        BatteryPercent = info.Percent;
        Charging = info.Status is "Charging" or "Full";
        BatteryStatus = info.Status == "Not charging" ? "Plugged in" : info.Status;
        BatteryTime = info.HoursLeft is { } h
            ? info.Status == "Charging" ? $"{Probe.Duration(h)} to full" : $"{Probe.Duration(h)} left"
            : "";
        var parts = new List<string>();
        if (info.Watts is { } w) parts.Add($"{w.ToString("0.0", CultureInfo.InvariantCulture)} W");
        if (info.Health is { } health) parts.Add($"health {Math.Round(health * 100)}%");
        if (info.Cycles > 0) parts.Add($"{info.Cycles} cycles");
        BatteryDetail = string.Join("  ·  ", parts);
    }

    static string ReadOr(string path, string fallback)
    {
        try { return File.ReadAllText(path).Trim(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return fallback; }
    }

    void Redraw()
    {
        var grid = Theme.Color("muted").WithAlpha(0.35);
        var caption = Theme.Color("foreground").WithAlpha(0.5);
        Draw("cpu", (d, w, h) =>
        {
            Graph.Area(d, [.. _cpuHistory], Samples, 100, w, h, Theme.Color("accent"), 0.2);
            Graph.Caption(d, "100%", caption);
        });
        Draw("memory", (d, w, h) =>
        {
            Graph.Area(d, [.. _memHistory], Samples, 100, w, h, Theme.Color("magenta", "#bb9af7"), 0.2);
            Graph.Caption(d, "100%", caption);
        });
        Draw("network", (d, w, h) =>
        {
            var top = Probe.NiceCeiling(Math.Max(64 * 1024, _downHistory.Concat(_upHistory).DefaultIfEmpty(0).Max()));
            Graph.Area(d, [.. _downHistory], Samples, top, w, h, Theme.Color("brightCyan", "#7dcfff"), 0.18);
            Graph.Area(d, [.. _upHistory], Samples, top, w, h, Theme.Color("yellow", "#e0af68"), 0.1);
            Graph.Caption(d, Probe.Rate(top), caption);
        });
        Draw("temp", (d, w, h) =>
        {
            var heat = CpuTemp / TempLimit;
            var color = heat > 0.9 ? Theme.Color("red", "#f7768e") : heat > 0.75 ? Theme.Color("yellow", "#e0af68") : Theme.Color("green", "#9ece6a");
            Graph.Area(d, [.. _tempHistory], Samples, TempLimit, w, h, color, 0.2);
            Graph.Caption(d, $"{TempLimit:0}°C", caption);
        });

        void Draw(string name, Action<Drawing, double, double> paint)
        {
            if (!_sizes.TryGetValue(name, out var size) || size.W <= 0 || size.H <= 0) return;
            var d = new Drawing();
            Graph.Grid(d, size.W, size.H, grid);
            paint(d, size.W, size.H);
            Bridge.Show(name, d);
        }
    }
}
