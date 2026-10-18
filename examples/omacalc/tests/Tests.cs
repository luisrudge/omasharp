// omacalc's tests/tst_omacalc.cpp, translated mechanically to C#.
using OmacalcCs;
static partial class Tests
{
    static int pass, fail;
    static void Press(Backend c, string keys) { foreach (var k in keys.Split(' ', StringSplitOptions.RemoveEmptyEntries)) c.PressKey(k); }
    static void Eq(string test, string actual, string expected)
    {
        if (actual == expected) pass++;
        else { fail++; Console.WriteLine($"FAIL {test}: got '{actual}', expected '{expected}'"); }
    }
    static int Main()
    {
        startsAtZero();
        calculatesPowers();
        calculatesWithPrecedence();
        showsEntryWhileTyping();
        handlesDecimals();
        divisionByZeroErrors();
        percentOfRunningTotal();
        percentAndSign();
        signStartsNewOperand();
        chainsWithFullPrecision();
        capsEntryAtFifteenDigits();
        backspaceEdits();
        chainsFromResult();
        replacesDanglingOperator();
        pastesNumbers();
        evaluatesTokens();
        opensAtTheDesignSizeForTheTextScale();
        dropsASavedWindowRectangle();
        normalizesThePortalTextScale();
        readsThePortalTextScaleWithoutStalling();
        formatsNumbers();
        loadsCurrentOmarchyTheme();
        Extra();
        Console.WriteLine($"{pass} passed, {fail} failed");
        return fail == 0 ? 0 : 1;
    }
    static void startsAtZero()
    {
        var c = new Backend();
        Eq("startsAtZero", c.Display, "0");
        Eq("startsAtZero", c.Expression, "");
    }

    static void calculatesPowers()
    {
        var c = new Backend();
        Press(c, "2 ^ 2 =");
        Eq("calculatesPowers", c.Display, "4");
        Press(c, "clear 2 × 3 ^ 2 =");
        Eq("calculatesPowers", c.Display, "18");
        Press(c, "clear 2 ^ 3 × 2 =");
        Eq("calculatesPowers", c.Display, "16");
        Press(c, "clear 2 ^ 3 ^ 2 =");
        Eq("calculatesPowers", c.Display, "512");
        Press(c, "clear 2 ^ 0 =");
        Eq("calculatesPowers", c.Display, "1");
        Press(c, "clear 2 ^ 3 = ^ 2 =");
        Eq("calculatesPowers", c.Display, "64");
        Press(c, "clear 2 sign ^ 3 =");
        Eq("calculatesPowers", c.Display, "-8");
        Press(c, "clear 0 ^ 0 =");
        Eq("calculatesPowers", c.Display, "1");
        Press(c, "clear 0 ^ 1 sign =");
        Eq("calculatesPowers", c.Display, "Error");
        Press(c, "clear 2 sign ^ 0 . 5 =");
        Eq("calculatesPowers", c.Display, "Error");
        Press(c, "5");
        Eq("calculatesPowers", c.Display, "5");
    }

    static void calculatesWithPrecedence()
    {
        var c = new Backend();
        Press(c, "4 2 × 3 + 7 =");
        Eq("calculatesWithPrecedence", c.Display, "133");
        Eq("calculatesWithPrecedence", c.Expression, "42 × 3 + 7");
        Press(c, "clear 2 + 3 × 4 =");
        Eq("calculatesWithPrecedence", c.Display, "14");
        Press(c, "clear 1 0 - 4 ÷ 2 =");
        Eq("calculatesWithPrecedence", c.Display, "8");
    }

    static void showsEntryWhileTyping()
    {
        var c = new Backend();
        Press(c, "4 2 ×");
        Eq("showsEntryWhileTyping", c.Display, "42");
        Eq("showsEntryWhileTyping", c.Expression, "42 ×");
        Press(c, "3");
        Eq("showsEntryWhileTyping", c.Display, "3");
    }

    static void handlesDecimals()
    {
        var c = new Backend();
        Press(c, ". 5 + . 2 5 =");
        Eq("handlesDecimals", c.Display, "0.75");
        Press(c, "clear 0 . 1 + 0 . 2 =");
        Eq("handlesDecimals", c.Display, "0.3");
        Press(c, "clear 1 . 5 . 5");
        Eq("handlesDecimals", c.Display, "1.55");
    }

    static void divisionByZeroErrors()
    {
        var c = new Backend();
        Press(c, "1 ÷ 0 =");
        Eq("divisionByZeroErrors", c.Display, "Error");
        Press(c, "5");
        Eq("divisionByZeroErrors", c.Display, "5");
    }

    static void percentOfRunningTotal()
    {
        var c = new Backend();
        Press(c, "2 0 0 + 1 0 % =");
        Eq("percentOfRunningTotal", c.Display, "220");
        Press(c, "clear 2 0 0 - 1 0 % =");
        Eq("percentOfRunningTotal", c.Display, "180");
        Press(c, "clear 2 0 0 × 1 0 % =");
        Eq("percentOfRunningTotal", c.Display, "20");
        Press(c, "clear 2 ^ 1 0 % =");
        // skipped: QCOMPARE(calculator.display(), Backend::formatNumber(std::pow(2.0, 0.1)));
        Press(c, "clear 5 0 %");
        Eq("percentOfRunningTotal", c.Display, "0.5");
        Press(c, "clear 4 0 + 1 0 = %");
        Eq("percentOfRunningTotal", c.Display, "0.5");
    }

    static void percentAndSign()
    {
        var c = new Backend();
        Press(c, "8 sign");
        Eq("percentAndSign", c.Display, "-8");
        Press(c, "sign");
        Eq("percentAndSign", c.Display, "8");
        Press(c, "clear 4 + 8 sign =");
        Eq("percentAndSign", c.Display, "-4");
    }

    static void signStartsNewOperand()
    {
        var c = new Backend();
        Press(c, "4 + sign 2 =");
        Eq("signStartsNewOperand", c.Display, "2");
        Eq("signStartsNewOperand", c.Expression, "4 + -2");
        Press(c, "clear sign");
        Eq("signStartsNewOperand", c.Display, "-0");
        Press(c, "5");
        Eq("signStartsNewOperand", c.Display, "-5");
    }

    static void chainsWithFullPrecision()
    {
        var c = new Backend();
        Press(c, "1 ÷ 3 = × 3 =");
        Eq("chainsWithFullPrecision", c.Display, "1");
        Press(c, "clear 9 9 9 9 9 9 9 9 9 9 9 9 9 9 =");
        Eq("chainsWithFullPrecision", c.Display, "99999999999999");
    }

    static void capsEntryAtFifteenDigits()
    {
        var c = new Backend();
        Press(c, "1 2 3 4 5 6 7 8 9 1 2 3 4 5 6 7 8");
        Eq("capsEntryAtFifteenDigits", c.Display, "123456789123456");
        Press(c, "clear . 1 2 3 4 5 6 7 8 9 1 2 3 4 5 6 7");
        Eq("capsEntryAtFifteenDigits", c.Display, "0.123456789123456");
    }

    static void backspaceEdits()
    {
        var c = new Backend();
        Press(c, "1 2 3 backspace");
        Eq("backspaceEdits", c.Display, "12");
        Press(c, "backspace backspace backspace");
        Eq("backspaceEdits", c.Display, "0");
    }

    static void chainsFromResult()
    {
        var c = new Backend();
        Press(c, "6 × 7 = × 2 =");
        Eq("chainsFromResult", c.Display, "84");
        Eq("chainsFromResult", c.Expression, "42 × 2");
        Press(c, "9");
        Eq("chainsFromResult", c.Display, "9");
        Eq("chainsFromResult", c.Expression, "");
    }

    static void replacesDanglingOperator()
    {
        var c = new Backend();
        Press(c, "4 + × 2 =");
        Eq("replacesDanglingOperator", c.Display, "8");
        Press(c, "clear 9 + =");
        Eq("replacesDanglingOperator", c.Display, "9");
    }

    static void pastesNumbers()
    {
        var c = new Backend();
        // The C++ test sets the real clipboard; here the clipboard text goes straight in.
        c.PasteText(" 42.5 ");
        Eq("pastesNumbers", c.Display, "42.5");
        Press(c, "+ . 5 =");
        Eq("pastesNumbers", c.Display, "43");
        c.PasteText("1,5");
        Eq("pastesNumbers", c.Display, "1.5");
        c.PasteText("not a number");
        Eq("pastesNumbers", c.Display, "1.5");
    }

    static void evaluatesTokens()
    {
        var c = new Backend();
        // skipped: bool ok = false;
        // skipped: QCOMPARE(Backend::evaluateTokens({"2", "+", "3", "×", "4"}, &ok), 14.0);
        // skipped: QVERIFY(ok);
        // skipped: QCOMPARE(Backend::evaluateTokens({"2", "^", "3"}, &ok), 8.0);
        // skipped: QVERIFY(ok);
        // skipped: QCOMPARE(Backend::evaluateTokens({"2", "^", "3", "^", "2"}, &ok), 512.0);
        // skipped: QVERIFY(ok);
        // skipped: Backend::evaluateTokens({"2", "+"}, &ok);
        // skipped: QVERIFY(!ok);
        // skipped: Backend::evaluateTokens({"1", "÷", "0"}, &ok);
        // skipped: QVERIFY(!ok);
    }

    static void opensAtTheDesignSizeForTheTextScale()
    {
        var c = new Backend();
        // skipped: calculator.setTextScale(1);
        // skipped: QVariantMap size = calculator.defaultWindowSize();
        // skipped: QCOMPARE(size.value(QStringLiteral("width")).toInt(), 400);
        // skipped: QCOMPARE(size.value(QStringLiteral("height")).toInt(), 568);
        // skipped: calculator.setTextScale(1.5);
        // skipped: size = calculator.defaultWindowSize();
        // skipped: QCOMPARE(size.value(QStringLiteral("width")).toInt(), 600);
        // skipped: QCOMPARE(size.value(QStringLiteral("height")).toInt(), 852);
        // skipped: calculator.setTextScale(0.5);
        // skipped: size = calculator.defaultWindowSize();
        // skipped: QCOMPARE(size.value(QStringLiteral("width")).toInt(), 300);
        // skipped: QCOMPARE(size.value(QStringLiteral("height")).toInt(), 430);
        // skipped: calculator.setTextScale(qQNaN());
        // skipped: size = calculator.defaultWindowSize();
        // skipped: QCOMPARE(size.value(QStringLiteral("width")).toInt(), 400);
        // skipped: QCOMPARE(size.value(QStringLiteral("height")).toInt(), 568);
    }

    static void dropsASavedWindowRectangle()
    {
        var c = new Backend();
        // skipped: QSettings written;
        // skipped: written.setValue(QStringLiteral("window/geometry"), QRect(0, 0, 1896, 1030));
        // skipped: written.setValue(QStringLiteral("window/maximized"), true);
        // skipped: written.setValue(QStringLiteral("unrelated"), QStringLiteral("kept"));
        // skipped: written.sync();
        // skipped: Q_UNUSED(calculator);
        // skipped: QSettings stored;
        // skipped: QVERIFY(!stored.contains(QStringLiteral("window/geometry")));
        // skipped: QVERIFY(!stored.contains(QStringLiteral("window/maximized")));
        // skipped: QCOMPARE(stored.value(QStringLiteral("unrelated")).toString(), QStringLiteral("kept"));
    }

    static void normalizesThePortalTextScale()
    {
        var c = new Backend();
        // skipped: bool known = false;
        // skipped: QCOMPARE(SystemTheme::normalizedTextScale(QVariant(1.25), &known), 1.25);
        // skipped: QVERIFY(known);
        // skipped: known = false;
        // skipped: QCOMPARE(SystemTheme::normalizedTextScale(QVariant(4.0), &known), 3.0);
        // skipped: QVERIFY(known);
        // skipped: known = false;
        // skipped: QCOMPARE(SystemTheme::normalizedTextScale(QVariant(0.1), &known), 0.5);
        // skipped: QVERIFY(known);
        // skipped: known = false;
        // skipped: QCOMPARE(SystemTheme::normalizedTextScale(QVariant(0.0), &known), 1.0);
        // skipped: QVERIFY(!known);
        // skipped: known = false;
        // skipped: QCOMPARE(SystemTheme::normalizedTextScale(QVariant(QStringLiteral("nope")), &known), 1.0);
        // skipped: QVERIFY(!known);
    }

    static void readsThePortalTextScaleWithoutStalling()
    {
        var c = new Backend();
        // skipped: QElapsedTimer timer;
        // skipped: timer.start();
        // skipped: SystemTheme theme;
        // skipped: QVERIFY(timer.elapsed() < 2000);
        // skipped: QVERIFY(theme.textScale() >= 0.5);
        // skipped: QVERIFY(theme.textScale() <= 3.0);
    }

    static void formatsNumbers()
    {
        var c = new Backend();
        // skipped: QCOMPARE(Backend::formatNumber(133), QStringLiteral("133"));
        // skipped: QCOMPARE(Backend::formatNumber(0.1 + 0.2), QStringLiteral("0.3"));
        // skipped: QCOMPARE(Backend::formatNumber(-0.0), QStringLiteral("0"));
        // skipped: QCOMPARE(Backend::formatNumber(1e15), QStringLiteral("1e+15"));
    }

    static void loadsCurrentOmarchyTheme()
    {
        var c = new Backend();
        // skipped: QTemporaryDir homeDirectory;
        // skipped: QVERIFY(homeDirectory.isValid());
        // skipped: const QByteArray originalHome = qgetenv("HOME");
        // skipped: struct HomeRestorer {
        // skipped: QByteArray value;
        // skipped: ~HomeRestorer() { qputenv("HOME", value); }
        // skipped: } restoreHome{originalHome};
        // skipped: QVERIFY(qputenv("HOME", homeDirectory.path().toUtf8()));
        // skipped: const QString themeDirectory = homeDirectory.path()
        // skipped: + QStringLiteral("/.local/state/omarchy/current/theme");
        // skipped: QVERIFY(QDir().mkpath(themeDirectory));
        // skipped: QFile colorsFile(themeDirectory + QStringLiteral("/colors.toml"));
        // skipped: QVERIFY(colorsFile.open(QIODevice::WriteOnly | QIODevice::Text));
        // skipped: const QByteArray palette(
        // skipped: "mode = \"light\" # a light theme\n"
        // skipped: "accent = \"#112233\" # button accent\n"
        // skipped: "selection = \"#445566\" # selection color\n"
        // skipped: "background = \"#fefefe\" # canvas\n"
        // skipped: "foreground = \"#101010\" # text\n");
        // skipped: QCOMPARE(colorsFile.write(palette), qint64(palette.size()));
        // skipped: colorsFile.close();
        // skipped: QCOMPARE(calculator.themeBackground(), QStringLiteral("#fefefe"));
        // skipped: QCOMPARE(calculator.themeForeground(), QStringLiteral("#101010"));
        // skipped: QCOMPARE(calculator.themeAccent(), QStringLiteral("#112233"));
        // skipped: QCOMPARE(calculator.themeSelection(), QStringLiteral("#445566"));
        // skipped: QVERIFY(!calculator.darkMode());
        // skipped: QVERIFY(colorsFile.open(QIODevice::WriteOnly | QIODevice::Truncate | QIODevice::Text));
        // skipped: const QByteArray invalidPalette(
        // skipped: "mode = \"light\"\n"
        // skipped: "background = \"not a color\" # invalid canvas\n"
        // skipped: "foreground = #101010 # bare hex text\n");
        // skipped: QCOMPARE(colorsFile.write(invalidPalette), qint64(invalidPalette.size()));
        // skipped: colorsFile.close();
        // skipped: Backend withInvalidColor;
        // skipped: QCOMPARE(withInvalidColor.themeBackground(), QStringLiteral("#ffffff"));
        // skipped: QCOMPARE(withInvalidColor.themeForeground(), QStringLiteral("#101010"));
        // skipped: QCOMPARE(withInvalidColor.themeAccent(), QStringLiteral("#2077b2"));
        // skipped: QCOMPARE(withInvalidColor.themeSelection(), QStringLiteral("#2077b2"));
        // skipped: QVERIFY(!withInvalidColor.darkMode());
    }
}
