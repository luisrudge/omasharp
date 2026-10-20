// Tests for Backend.cs and Probe.cs. Plain C#: no Qt, no window. Run: dotnet run --project tests
// The bridge code refers to the app's backend class as AppBackend (the app build generates
// this alias from <BackendType> in the csproj; the tests have to say it themselves).
global using AppBackend = Backend;

var failures = 0;
void Check(string name, bool ok)
{
    Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {name}");
    if (!ok) failures++;
}

var stat1 = Probe.ParseStat("cpu  100 0 100 800 0 0 0 0 0 0\ncpu0 50 0 50 400 0 0 0 0 0 0\ncpu1 50 0 50 400 0 0 0 0 0 0\nintr 1 2 3\n");
var stat2 = Probe.ParseStat("cpu  150 0 150 900 0 0 0 0 0 0\ncpu0 100 0 100 400 0 0 0 0 0 0\ncpu1 50 0 50 500 0 0 0 0 0 0\nintr 1 2 3\n");
Check("stat: total line plus one per core", stat1.Count == 3);
Check("cpu busy share", Math.Abs(Probe.Busy(stat1[0], stat2[0]) - 50) < 1e-9);
Check("per core: one busy, one idle", Probe.Busy(stat1[1], stat2[1]) == 100 && Probe.Busy(stat1[2], stat2[2]) == 0);
Check("no time passed is 0%", Probe.Busy(stat1[0], stat1[0]) == 0);

var mem = Probe.ParseMeminfo("MemTotal:       16000 kB\nMemAvailable:    4000 kB\nHugePages_Total:       0\n");
Check("meminfo in bytes", mem["MemTotal"] == 16000 * 1024 && mem["MemAvailable"] == 4000 * 1024 && mem["HugePages_Total"] == 0);

var net = Probe.ParseNetDev("Inter-|   Receive\n face |bytes\n    lo: 10 1 0 0 0 0 0 0 20 2 0 0 0 0 0 0\nwlp3s0: 3000 9 0 0 0 0 0 0 4000 7 0 0 0 0 0 0\n");
Check("net/dev rx and tx", net["wlp3s0"] == (3000, 4000) && net["lo"] == (10, 20));

var mounts = Probe.ParseMounts("/dev/mapper/root /home btrfs rw 0 0\n/dev/mapper/root / btrfs rw 0 0\nproc /proc proc rw 0 0\n/dev/loop0 /snap/x squashfs ro 0 0\n/dev/sda1 /boot vfat rw 0 0\n/dev/sdb1 /mnt/My\\040Disk ext4 rw 0 0\n");
Check("mounts: one per device, shortest path, no loops", mounts.SequenceEqual([("/dev/mapper/root", "/"), ("/dev/sda1", "/boot"), ("/dev/sdb1", "/mnt/My Disk")]));

var charge = Probe.ParseBattery("POWER_SUPPLY_TYPE=Battery\nPOWER_SUPPLY_STATUS=Discharging\nPOWER_SUPPLY_PRESENT=1\nPOWER_SUPPLY_VOLTAGE_NOW=12000000\nPOWER_SUPPLY_CURRENT_NOW=1000000\nPOWER_SUPPLY_CHARGE_FULL_DESIGN=6000000\nPOWER_SUPPLY_CHARGE_FULL=5400000\nPOWER_SUPPLY_CHARGE_NOW=3000000\nPOWER_SUPPLY_CAPACITY=55\nPOWER_SUPPLY_CYCLE_COUNT=58\n");
Check("charge battery: 3 h left at 1 A", charge is { Percent: 55, HoursLeft: 3, Cycles: 58 });
Check("charge battery: watts and health", Math.Abs(charge!.Watts!.Value - 12) < 1e-9 && Math.Abs(charge.Health!.Value - 0.9) < 1e-9);
var energy = Probe.ParseBattery("POWER_SUPPLY_TYPE=Battery\nPOWER_SUPPLY_STATUS=Charging\nPOWER_SUPPLY_ENERGY_FULL=50000000\nPOWER_SUPPLY_ENERGY_NOW=40000000\nPOWER_SUPPLY_POWER_NOW=20000000\nPOWER_SUPPLY_CAPACITY=80\n");
Check("energy battery: half an hour to full at 20 W", energy is { HoursLeft: 0.5, Watts: 20 });
Check("trickle gives no estimate", Probe.ParseBattery("POWER_SUPPLY_TYPE=Battery\nPOWER_SUPPLY_STATUS=Discharging\nPOWER_SUPPLY_CHARGE_NOW=5000000\nPOWER_SUPPLY_CURRENT_NOW=40000\n") is { HoursLeft: null });
Check("mains adapter is not a battery", Probe.ParseBattery("POWER_SUPPLY_TYPE=Mains\nPOWER_SUPPLY_ONLINE=1\n") is null);

var intel = new List<Probe.Sensor> { new("acpitz", "", 50), new("coretemp", "Core 0", 70, Crit: 100), new("coretemp", "Package id 0", 72, Crit: 100) };
Check("intel: the package sensor", Probe.PickCpuTemp(intel)?.Value == 72);
var amd = new List<Probe.Sensor> { new("nvme", "Composite", 40), new("k10temp", "Tctl", 65), new("k10temp", "Tccd1", 60) };
Check("amd: Tctl", Probe.PickCpuTemp(amd)?.Value == 65);
Check("no cpu sensor", Probe.PickCpuTemp([new("acpitz", "", 50)]) is null);
Check("fan between its min and max", Probe.FanFraction(new("applesmc", "Right Side", 3749, Min: 1299, Max: 6199)) == 0.5);
Check("fan without a range", Probe.FanFraction(new("thinkpad", "", 2000)) == -1);

Check("bytes", Probe.Bytes(512) == "512 B" && Probe.Bytes(1536) == "1.50 KiB" && Probe.Bytes(115.0 * (1 << 30)) == "115 GiB");
Check("durations", Probe.Duration(3.2) == "3h 12m" && Probe.Duration(0.5) == "30m");
Check("uptime", Probe.Uptime(129568) == "1d 11h" && Probe.Uptime(3700) == "1h 1m");
Check("nice scale tops", Probe.NiceCeiling(70 * 1024) == 100 * 1024 && Probe.NiceCeiling(3 * 1024 * 1024) == 5 * 1024 * 1024 && Probe.NiceCeiling(600 * 1024) == 1024 * 1024);

// The real machine: two ticks give one CPU and network sample and sane readings.
var b = new Backend();
b.Tick();
Check("cpu reading in range", b.CpuPercent is >= 0 and <= 100 && b.CpuHistory.Count == 1 && b.Cores.Count == Probe.ParseStat(File.ReadAllText("/proc/stat")).Count - 1);
Check("memory reading", b.MemPercent is > 0 and < 100 && b.MemUsed.Contains(" of "));
Check("network sample", b.DownHistory.Count == 1 && b.Down.EndsWith("/s"));
Check("cpu temperature read", !b.HasTemp || (b.CpuTemp is > 0 and < 150 && b.TempHistory.Count == 2));
// In a container, / is an overlay rather than a block device, and isn't listed.
var rootIsDevice = Probe.ParseMounts(File.ReadAllText("/proc/mounts")).Any(m => m.Mount == "/");
Check("root volume listed", !rootIsDevice || b.Disks.Any(d => d.Mount == "/" && d.Fraction is > 0 and < 1));
for (var i = 0; i < Backend.Samples + 10; i++) b.Tick();
Check("history is capped", b.CpuHistory.Count == Backend.Samples && b.MemHistory.Count == Backend.Samples);

Console.WriteLine(failures == 0 ? "all passed" : $"{failures} failed");
return failures == 0 ? 0 : 1;
