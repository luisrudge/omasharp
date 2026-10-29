using OmacalcCs;
static partial class Tests
{
    static void Extra()
    {
        Eq("evaluate", Backend.EvaluateTokens(["2", "+", "3", "×", "4"])?.ToString() ?? "null", "14");
        Eq("evaluate", Backend.EvaluateTokens(["2", "^", "3"])?.ToString() ?? "null", "8");
        Eq("evaluate", Backend.EvaluateTokens(["2", "^", "3", "^", "2"])?.ToString() ?? "null", "512");
        Eq("evaluate", Backend.EvaluateTokens(["2", "+"])?.ToString() ?? "null", "null");
        Eq("evaluate", Backend.EvaluateTokens(["1", "÷", "0"])?.ToString() ?? "null", "null");

        Eq("format", Backend.FormatNumber(133), "133");
        Eq("format", Backend.FormatNumber(0.1 + 0.2), "0.3");
        Eq("format", Backend.FormatNumber(-0.0), "0");
        Eq("format", Backend.FormatNumber(1e15), "1e+15");
        var c = new Backend();
        Press(c, "clear 2 ^ 1 0 % =");
        Eq("percentPower", c.Display, Backend.FormatNumber(Math.Pow(2.0, 0.1)));

        var home = Directory.CreateTempSubdirectory("omacalc-cs-test").FullName;
        var originalHome = Environment.GetEnvironmentVariable("HOME");
        Environment.SetEnvironmentVariable("HOME", home);
        try
        {
            var themeDir = Path.Combine(home, ".local/state/omarchy/current/theme");
            Directory.CreateDirectory(themeDir);
            var colors = Path.Combine(themeDir, "colors.toml");
            File.WriteAllText(colors,
                "mode = \"light\" # a light theme\n" +
                "accent = \"#112233\" # button accent\n" +
                "selection = \"#445566\" # selection color\n" +
                "background = \"#fefefe\" # canvas\n" +
                "foreground = \"#101010\" # text\n");
            var t = new Backend();
            Eq("theme", t.ThemeBackground, "#fefefe");
            Eq("theme", t.ThemeForeground, "#101010");
            Eq("theme", t.ThemeAccent, "#112233");
            Eq("theme", t.ThemeSelection, "#445566");
            Eq("theme", t.DarkMode.ToString(), "False");

            File.WriteAllText(colors,
                "mode = \"light\"\n" +
                "background = \"not a color\" # invalid canvas\n" +
                "foreground = #101010 # bare hex text\n");
            var invalid = new Backend();
            Eq("themeInvalid", invalid.ThemeBackground, "#ffffff");
            Eq("themeInvalid", invalid.ThemeForeground, "#101010");
            Eq("themeInvalid", invalid.ThemeAccent, "#2077b2");
            Eq("themeInvalid", invalid.ThemeSelection, "#2077b2");
            Eq("themeInvalid", invalid.DarkMode.ToString(), "False");
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            Directory.Delete(home, true);
        }
    }
}
