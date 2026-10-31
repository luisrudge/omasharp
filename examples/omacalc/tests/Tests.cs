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
        c.PasteText(" 42.5 ");
        Eq("pastesNumbers", c.Display, "42.5");
        Press(c, "+ . 5 =");
        Eq("pastesNumbers", c.Display, "43");
        c.PasteText("1,5");
        Eq("pastesNumbers", c.Display, "1.5");
        c.PasteText("not a number");
        Eq("pastesNumbers", c.Display, "1.5");
    }
}
