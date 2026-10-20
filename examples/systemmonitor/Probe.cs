// Parsers for the kernel's text files (/proc, /sys) and the formatting the UI shows.
// Pure functions of their input, so the tests can feed them sample text.
using System.Globalization;

static class Probe
{
    internal readonly record struct CpuTimes(ulong Idle, ulong Total);

    /// <summary>/proc/stat: the "cpu" total line first, then one per core.</summary>
    internal static List<CpuTimes> ParseStat(string text)
    {
        var list = new List<CpuTimes>();
        foreach (var line in text.Split('\n'))
        {
            if (!line.StartsWith("cpu", StringComparison.Ordinal)) continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5) continue;
            // user nice system idle iowait irq softirq steal (guest time is already in user)
            ulong total = 0;
            for (var i = 1; i < Math.Min(parts.Length, 9); i++) total += ulong.Parse(parts[i], CultureInfo.InvariantCulture);
            var idle = ulong.Parse(parts[4], CultureInfo.InvariantCulture)
                + (parts.Length > 5 ? ulong.Parse(parts[5], CultureInfo.InvariantCulture) : 0);
            list.Add(new CpuTimes(idle, total));
        }
        return list;
    }

    /// <summary>Busy percentage between two readings of the same CPU.</summary>
    internal static double Busy(CpuTimes before, CpuTimes after)
    {
        var total = (double)after.Total - before.Total;
        var idle = (double)after.Idle - before.Idle;
        return total <= 0 ? 0 : Math.Clamp(100 * (total - idle) / total, 0, 100);
    }

    /// <summary>/proc/meminfo in bytes, by key ("MemTotal", "MemAvailable", ...).</summary>
    internal static Dictionary<string, double> ParseMeminfo(string text)
    {
        var map = new Dictionary<string, double>();
        foreach (var line in text.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon < 0) continue;
            var value = line[(colon + 1)..].Trim().Split(' ');
            if (double.TryParse(value[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                map[line[..colon]] = value.Length > 1 && value[1] == "kB" ? v * 1024 : v;
        }
        return map;
    }

    /// <summary>/proc/net/dev: received and sent bytes per interface.</summary>
    internal static Dictionary<string, (double Rx, double Tx)> ParseNetDev(string text)
    {
        var map = new Dictionary<string, (double, double)>();
        foreach (var line in text.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon < 0) continue;
            var f = line[(colon + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (f.Length < 9) continue;
            map[line[..colon].Trim()] = (double.Parse(f[0], CultureInfo.InvariantCulture), double.Parse(f[8], CultureInfo.InvariantCulture));
        }
        return map;
    }

    /// <summary>/proc/mounts: real block devices (no loop devices), each once, at its shortest mount path.</summary>
    internal static List<(string Device, string Mount)> ParseMounts(string text)
    {
        var byDevice = new Dictionary<string, string>();
        var order = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            var f = line.Split(' ');
            if (f.Length < 2 || !f[0].StartsWith("/dev/", StringComparison.Ordinal) || f[0].StartsWith("/dev/loop", StringComparison.Ordinal)) continue;
            var mount = f[1].Replace("\\040", " ", StringComparison.Ordinal);
            if (!byDevice.TryGetValue(f[0], out var seen)) order.Add(f[0]);
            else if (seen.Length <= mount.Length) continue;
            byDevice[f[0]] = mount;
        }
        return [.. order.Select(d => (d, byDevice[d]))];
    }

    internal sealed record BatteryInfo(int Percent, string Status, double? HoursLeft, double? Health, int Cycles, double? Watts);

    /// <summary>A /sys/class/power_supply/BAT*/uevent file. Batteries report energy (µWh, µW) or charge (µAh, µA).</summary>
    internal static BatteryInfo? ParseBattery(string uevent)
    {
        var v = new Dictionary<string, string>();
        foreach (var line in uevent.Split('\n'))
        {
            var eq = line.IndexOf('=');
            if (eq > 0) v[line[..eq].Replace("POWER_SUPPLY_", "", StringComparison.Ordinal)] = line[(eq + 1)..].Trim();
        }
        double? Num(string key) => v.TryGetValue(key, out var s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
        if (v.GetValueOrDefault("TYPE") != "Battery" || v.GetValueOrDefault("PRESENT") == "0") return null;

        var unit = Num("ENERGY_NOW") is not null ? "ENERGY" : "CHARGE";
        double? now = Num(unit + "_NOW"), full = Num(unit + "_FULL"), design = Num(unit + "_FULL_DESIGN");
        var rate = Math.Abs((unit == "ENERGY" ? Num("POWER_NOW") : Num("CURRENT_NOW")) ?? 0);
        var status = v.GetValueOrDefault("STATUS", "Unknown");
        var percent = (int)(Num("CAPACITY") ?? (now is not null && full > 0 ? 100 * now / full : 0) ?? 0);

        double? hours = null;
        // A trickle (charger holding at its limit, sensor noise) gives estimates of days: leave those out.
        if (rate > 0 && now is not null)
        {
            if (status == "Discharging") hours = now / rate;
            else if (status == "Charging" && full is not null) hours = Math.Max(0, full.Value - now.Value) / rate;
        }
        if (hours > 24) hours = null;
        var voltage = Num("VOLTAGE_NOW");
        double? watts = rate <= 0 ? null : unit == "ENERGY" ? rate / 1e6 : voltage is null ? null : rate * voltage / 1e12;
        double? health = full > 0 && design > 0 ? Math.Min(1, full.Value / design.Value) : null;
        return new BatteryInfo(Math.Clamp(percent, 0, 100), status, hours, health, (int)(Num("CYCLE_COUNT") ?? 0), watts);
    }

    /// <summary>One hwmon reading: °C for temperatures, rpm for fans.</summary>
    internal sealed record Sensor(string Chip, string Label, double Value, double? Min = null, double? Max = null, double? Crit = null);

    /// <summary>The CPU temperature: Intel's package sensor, AMD's Tctl/Tdie, or an ARM SoC's cpu_thermal.</summary>
    internal static Sensor? PickCpuTemp(IReadOnlyList<Sensor> temps) =>
        temps.FirstOrDefault(t => t.Chip == "coretemp" && t.Label.StartsWith("Package", StringComparison.Ordinal))
        ?? temps.FirstOrDefault(t => t.Chip is "k10temp" or "zenpower" && t.Label == "Tdie")
        ?? temps.FirstOrDefault(t => t.Chip is "k10temp" or "zenpower" && t.Label == "Tctl")
        ?? temps.FirstOrDefault(t => t.Chip is "coretemp" or "k10temp" or "zenpower" or "cpu_thermal");

    /// <summary>How far a fan is between its slowest and fastest speed, or -1 when the driver doesn't say.</summary>
    internal static double FanFraction(Sensor fan) =>
        fan.Max is { } max && max > (fan.Min ?? 0) ? Math.Clamp((fan.Value - (fan.Min ?? 0)) / (max - (fan.Min ?? 0)), 0, 1) : -1;

    static readonly string[] s_units = ["B", "KiB", "MiB", "GiB", "TiB", "PiB"];

    internal static string Bytes(double bytes)
    {
        var i = 0;
        while (bytes >= 1024 && i < s_units.Length - 1) { bytes /= 1024; i++; }
        var format = i == 0 || bytes >= 100 ? "0" : bytes >= 10 ? "0.0" : "0.00";
        return bytes.ToString(format, CultureInfo.InvariantCulture) + " " + s_units[i];
    }

    internal static string Rate(double bytesPerSecond) => Bytes(bytesPerSecond) + "/s";

    internal static string Duration(double hours)
    {
        var minutes = (int)Math.Round(hours * 60);
        return minutes >= 60 ? $"{minutes / 60}h {minutes % 60:00}m" : $"{minutes}m";
    }

    internal static string Uptime(double seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        return t.Days > 0 ? $"{t.Days}d {t.Hours}h" : t.Hours > 0 ? $"{t.Hours}h {t.Minutes}m" : $"{t.Minutes}m";
    }

    /// <summary>A round top for a graph's scale: 1, 2, 5, 10 ... 500 of a power of 1024, at or above `value`.</summary>
    internal static double NiceCeiling(double value)
    {
        if (value <= 1) return 1;
        var unit = 1.0;
        while (value / unit >= 1024) unit *= 1024;
        foreach (var m in new[] { 1, 2, 5, 10, 20, 50, 100, 200, 500, 1024 })
            if (m * unit >= value) return m * unit;
        return 1024 * unit;
    }
}
