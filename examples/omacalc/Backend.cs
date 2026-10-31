using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace OmacalcCs;

public sealed class Backend : INotifyPropertyChanged, IDisposable
{
    const string Plus = "+", Minus = "−", Multiply = "×", Divide = "÷", Power = "^";
    static readonly CultureInfo C = CultureInfo.InvariantCulture;

    public event PropertyChangedEventHandler? PropertyChanged;

    readonly List<string> _tokens = [];
    string _entry = "";
    string _result = "";
    double _resultValue;
    string _evaluatedExpression = "";
    bool _justEvaluated;
    bool _errored;

    bool _darkMode = true;
    FileSystemWatcher? _themeWatcher;
    static string CurrentDir => Path.Combine(
        Environment.GetEnvironmentVariable("HOME") ?? "", ".local/state/omarchy/current");

    public Backend()
    {
        TextScale = ReadTextScale();
        LoadOmarchyTheme();
        WatchOmarchyTheme();
    }

    public string Expression => _errored || _justEvaluated ? _evaluatedExpression : PrettyExpression(_tokens);

    public string Display
    {
        get
        {
            if (_errored) return "Error";
            if (_entry.Length > 0) return _entry;
            if (_justEvaluated) return _result;
            return FormatNumber(ToDouble(CurrentValue()));
        }
    }

    public bool DarkMode => _darkMode;
    public double TextScale { get; private set; } = 1.0;
    public string ThemeBackground { get; private set; } = "#101010";
    public string ThemeForeground { get; private set; } = "#eeeeee";
    public string ThemeAccent { get; private set; } = "#5584aa";
    public string ThemeSelection { get; private set; } = "#186a9a";

    public int DefaultWidth => Math.Max(300, (int)Math.Round(400 * SafeScale));
    public int DefaultHeight => Math.Max(430, (int)Math.Round(568 * SafeScale));
    double SafeScale => double.IsFinite(TextScale) && TextScale > 0 ? TextScale : 1.0;

    public void PressKey(string key)
    {
        if (key.Length == 1 && char.IsDigit(key[0])) PressDigit(key);
        else if (key == ".") PressDecimal();
        else if (key == "+") PressOperator(Plus);
        else if (key == "-" || key == Minus) PressOperator(Minus);
        else if (key == "*" || key == Multiply) PressOperator(Multiply);
        else if (key == "/" || key == Divide) PressOperator(Divide);
        else if (key == "^") PressOperator(Power);
        else if (key == "=") PressEquals();
        else if (key == "%") PressPercent();
        else if (key == "sign") PressToggleSign();
        else if (key == "backspace") PressBackspace();
        else if (key == "clear") PressClear();
        else return;
        CalculationChanged();
    }

    public void CopyResult() => Run("wl-copy", Display);

    public void PasteNumber() => PasteText(Run("wl-paste", null, "--no-newline") ?? "");

    internal void PasteText(string raw)
    {
        var text = raw.Trim().Replace(Minus, "-").Replace(" ", "");
        if (!double.TryParse(text, NumberStyles.Float, C, out var value)
            && !double.TryParse(text.Replace(',', '.'), NumberStyles.Float, C, out value))
            return;
        if (!double.IsFinite(value)) return;
        if (_errored || _justEvaluated) PressClear();
        _entry = FormatNumber(value);
        CalculationChanged();
    }

    void PressDigit(string digit)
    {
        if (_errored) PressClear();
        if (_justEvaluated) PressClear();
        if (_entry == "0") { _entry = digit; return; }
        if (_entry == "-0") { _entry = "-" + digit; return; }
        var digits = _entry.Count(char.IsDigit);
        if (_entry.StartsWith("0.") || _entry.StartsWith("-0.")) digits--;
        if (digits < 15) _entry += digit;
    }

    void PressDecimal()
    {
        if (_errored) PressClear();
        if (_justEvaluated) PressClear();
        if (_entry.Length == 0) _entry = "0.";
        else if (!_entry.Contains('.')) _entry += ".";
    }

    void PressOperator(string pretty)
    {
        if (_errored) return;
        if (_justEvaluated)
        {
            _tokens.Clear();
            _tokens.Add(FormatG(_resultValue, 17));
            ClearEvaluation();
        }
        if (_entry.Length > 0) { _tokens.Add(SealNumber(_entry)); _tokens.Add(pretty); _entry = ""; }
        else if (_tokens.Count == 0) { _tokens.Add("0"); _tokens.Add(pretty); }
        else if (IsOperator(_tokens[^1])) _tokens[^1] = pretty;
        else _tokens.Add(pretty);
    }

    void PressEquals()
    {
        if (_errored || _justEvaluated) return;
        var final = new List<string>(_tokens);
        if (_entry.Length > 0) final.Add(SealNumber(_entry));
        while (final.Count > 0 && IsOperator(final[^1])) final.RemoveAt(final.Count - 1);
        if (final.Count == 0) return;
        _evaluatedExpression = PrettyExpression(final);
        if (EvaluateTokens(final) is { } value)
        {
            _resultValue = value;
            _result = FormatNumber(value);
            _justEvaluated = true;
        }
        else _errored = true;
        _tokens.Clear();
        _entry = "";
    }

    void PressPercent()
    {
        if (_errored) return;
        if (_justEvaluated)
        {
            var p = _resultValue / 100.0;
            ClearEvaluation();
            _entry = FormatNumber(p);
            return;
        }
        if (!double.TryParse(CurrentValue(), NumberStyles.Float, C, out var value)) return;
        var percent = value / 100.0;
        if (_tokens.Count > 0 && (_tokens[^1] == Plus || _tokens[^1] == Minus)
            && EvaluateTokens(_tokens[..^1]) is { } baseValue)
            percent = baseValue * value / 100.0;
        _entry = FormatNumber(percent);
    }

    void PressToggleSign()
    {
        if (_errored) return;
        if (_justEvaluated) BeginEditingAfterResult();
        if (_entry.Length == 0) { _entry = "-0"; return; }
        _entry = _entry.StartsWith('-') ? _entry[1..] : "-" + _entry;
    }

    void PressBackspace()
    {
        if (_errored) { PressClear(); return; }
        if (_justEvaluated) BeginEditingAfterResult();
        if (_entry.Length > 0) _entry = _entry[..^1];
        if (_entry == "-") _entry = "";
    }

    void PressClear()
    {
        _tokens.Clear();
        _entry = "";
        ClearEvaluation();
        _errored = false;
    }

    void BeginEditingAfterResult()
    {
        _entry = _result;
        _tokens.Clear();
        ClearEvaluation();
    }

    void ClearEvaluation()
    {
        _result = "";
        _resultValue = 0;
        _evaluatedExpression = "";
        _justEvaluated = false;
    }

    string CurrentValue()
    {
        if (_entry.Length > 0) return SealNumber(_entry);
        for (var i = _tokens.Count - 1; i >= 0; i--)
            if (!IsOperator(_tokens[i])) return _tokens[i];
        return "0";
    }

    internal static double? EvaluateTokens(IReadOnlyList<string> tokens)
    {
        if (tokens.Count % 2 == 0) return null;
        if (!double.TryParse(tokens[0], NumberStyles.Float, C, out var first)) return null;
        var values = new List<double> { first };
        var operators = new List<string>();
        for (var i = 1; i + 1 < tokens.Count; i += 2)
        {
            if (!double.TryParse(tokens[i + 1], NumberStyles.Float, C, out var operand) || !IsOperator(tokens[i]))
                return null;
            operators.Add(tokens[i]);
            values.Add(operand);
        }
        for (var i = operators.Count - 1; i >= 0; i--)
        {
            if (operators[i] != Power) continue;
            double b = values[i], e = values[i + 1];
            if (b < 0.0 && Math.Floor(e) != e) return null;
            var powered = Math.Pow(b, e);
            if (!double.IsFinite(powered)) return null;
            values[i] = powered;
            values.RemoveAt(i + 1);
            operators.RemoveAt(i);
        }
        var additive = new List<double> { values[0] };
        var additiveOps = new List<string>();
        for (var i = 0; i < operators.Count; i++)
        {
            var operand = values[i + 1];
            if (operators[i] == Multiply) additive[^1] *= operand;
            else if (operators[i] == Divide) additive[^1] /= operand;
            else { additiveOps.Add(operators[i]); additive.Add(operand); }
        }
        var total = additive[0];
        for (var i = 0; i < additiveOps.Count; i++)
            total = additiveOps[i] == Plus ? total + additive[i + 1] : total - additive[i + 1];
        return double.IsFinite(total) ? total : null;
    }

    internal static string FormatNumber(double value) => FormatG(value == 0 ? 0 : value, 15);

    static string FormatG(double value, int precision)
    {
        var s = value.ToString("G" + precision, C);
        var e = s.IndexOf('E');
        if (e < 0) return s;
        var exponent = s[(e + 2)..].TrimStart('0').PadLeft(2, '0');
        return s[..e] + "e" + s[e + 1] + exponent;
    }

    static string PrettyExpression(IEnumerable<string> tokens) =>
        string.Join(' ', tokens.Select(t => IsOperator(t) ? t : FormatNumber(ToDouble(t))));

    static bool IsOperator(string t) => t is Plus or Minus or Multiply or Divide or Power;

    static string SealNumber(string entry)
    {
        var sealed_ = entry.EndsWith('.') ? entry[..^1] : entry;
        return sealed_.Length == 0 || sealed_ == "-" ? "0" : sealed_;
    }

    static double ToDouble(string s) => double.TryParse(s, NumberStyles.Float, C, out var v) ? v : 0;

    void CalculationChanged()
    {
        Notify(nameof(Expression));
        Notify(nameof(Display));
    }

    void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));

    static double ReadTextScale()
    {
        var text = Run("gsettings", null, "get", "org.gnome.desktop.interface", "text-scaling-factor");
        return double.TryParse(text?.Trim(), NumberStyles.Float, C, out var scale) && scale > 0
            ? Math.Clamp(scale, 0.5, 3.0) : 1.0;
    }

    void LoadOmarchyTheme()
    {
        string? mode = null, background = null, foreground = null, accent = null, selection = null;
        var colors = Path.Combine(CurrentDir, "theme/colors.toml");
        if (File.Exists(colors))
        {
            foreach (var raw in File.ReadLines(colors))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var equals = line.IndexOf('=');
                if (equals < 0) continue;
                var key = line[..equals].Trim();
                var value = WithoutTomlComment(line[(equals + 1)..]);
                if (value.Length >= 2 && (value[0] == '"' && value[^1] == '"' || value[0] == '\'' && value[^1] == '\''))
                    value = value[1..^1];
                switch (key)
                {
                    case "mode": mode = value; break;
                    case "background" when IsColor(value): background = value; break;
                    case "foreground" when IsColor(value): foreground = value; break;
                    case "accent" when IsColor(value): accent = value; break;
                    case "selection" when IsColor(value): selection = value; break;
                }
            }
        }
        var dark = mode switch
        {
            "dark" => true,
            "light" => false,
            _ => background is { } bg ? Luminance(bg) < 0.5 : _darkMode,
        };
        if (dark != _darkMode) { _darkMode = dark; Notify(nameof(DarkMode)); }
        ThemeBackground = background ?? (dark ? "#101010" : "#ffffff");
        ThemeForeground = foreground ?? (dark ? "#eeeeee" : "#222324");
        ThemeAccent = accent ?? (dark ? "#5584aa" : "#2077b2");
        ThemeSelection = selection ?? (dark ? "#186a9a" : "#2077b2");
        Notify(nameof(ThemeBackground));
        Notify(nameof(ThemeForeground));
        Notify(nameof(ThemeAccent));
        Notify(nameof(ThemeSelection));
    }

    public void Dispose() => _themeWatcher?.Dispose();

    void WatchOmarchyTheme()
    {
        if (!Directory.Exists(CurrentDir)) return;
        _themeWatcher = new FileSystemWatcher(CurrentDir) { IncludeSubdirectories = true, EnableRaisingEvents = true };
        FileSystemEventHandler reload = (_, _) => LoadOmarchyTheme();
        _themeWatcher.Changed += reload;
        _themeWatcher.Created += reload;
        _themeWatcher.Renamed += (_, _) => LoadOmarchyTheme();
    }

    static string WithoutTomlComment(string text)
    {
        var value = text.Trim();
        char quote = '\0';
        var escaped = false;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (escaped) escaped = false;
            else if (quote == '"' && c == '\\') escaped = true;
            else if (quote != '\0' && c == quote) quote = '\0';
            else if (quote == '\0' && (c == '"' || c == '\'')) quote = c;
            else if (quote == '\0' && c == '#' && i > 0) return value[..i].Trim();
        }
        return value;
    }

    static bool IsColor(string v) =>
        v.Length is 4 or 7 or 9 && v[0] == '#' && v[1..].All(Uri.IsHexDigit);

    static double Luminance(string hex)
    {
        var h = hex[1..];
        if (h.Length == 3) h = string.Concat(h.Select(c => $"{c}{c}"));
        if (h.Length == 8) h = h[2..];
        double Channel(int i) => Convert.ToInt32(h.Substring(i, 2), 16) / 255.0;
        return 0.299 * Channel(0) + 0.587 * Channel(2) + 0.114 * Channel(4);
    }

    static string? Run(string file, string? input, params string[] args)
    {
        try
        {
            var start = new ProcessStartInfo(file) { RedirectStandardOutput = input == null, RedirectStandardInput = input != null };
            foreach (var a in args) start.ArgumentList.Add(a);
            using var process = Process.Start(start)!;
            if (input != null) { process.StandardInput.Write(input); process.StandardInput.Close(); return null; }
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(2000);
            return output;
        }
        catch { return null; }
    }
}
