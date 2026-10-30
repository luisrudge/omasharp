import contextlib
import io
import os
import sys
import tempfile
import unittest

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import comments




class Strip(unittest.TestCase):
    def check(self, name, src, want):
        self.assertEqual(comments.fix(name, src), want)
        self.assertEqual(comments.comments(name, want), [])

    def test_csharp(self):
        self.check("A.cs", (
            "#if OMASHARP_NATIVE\n"
            "/// <summary>Doc.</summary>\n"
            "var url = \"http://x\"; // gone\n"
            "var path = @\"C:\\\"; // gone\n"
            "var quote = '\"'; // gone\n"
            "var hole = $\"{F(\"//\")} /* kept */\"; /* gone */\n"
            "var raw = \"\"\"\n"
            "    // kept \"quoted\"\n"
            "    \"\"\";\n"
            "#pragma warning disable CA1822 // why\n"
            "#endif\n"
        ), (
            "#if OMASHARP_NATIVE\n"
            "var url = \"http://x\";\n"
            "var path = @\"C:\\\";\n"
            "var quote = '\"';\n"
            "var hole = $\"{F(\"//\")} /* kept */\";\n"
            "var raw = \"\"\"\n"
            "    // kept \"quoted\"\n"
            "    \"\"\";\n"
            "#pragma warning disable CA1822\n"
            "#endif\n"
        ))

    def test_cpp(self):
        self.check("a.cpp", (
            "auto s = R\"x(// kept )\" )x\"; /* gone\n"
            "   still gone */\n"
            "int n = 1'000; char c = '/'; // gone\n"
            "} // namespace\n"
        ), (
            "auto s = R\"x(// kept )\" )x\";\n"
            "int n = 1'000; char c = '/';\n"
            "}\n"
        ))

    def test_qml(self):
        self.check("Main.qml", (
            "if (/^[0-9+\\-*/%^]$/.test(t)) x = a / b // gone\n"
            "var u = `${\"//\"} // kept`; var v = 'it''s' /* gone */\n"
            "Text { text: \"#fff // kept\" } // gone\n"
            "var t = s.replace(/\\/*$/, \"\") // gone\n"
        ), (
            "if (/^[0-9+\\-*/%^]$/.test(t)) x = a / b\n"
            "var u = `${\"//\"} // kept`; var v = 'it''s'\n"
            "Text { text: \"#fff // kept\" }\n"
            "var t = s.replace(/\\/*$/, \"\")\n"
        ))

    def test_shell(self):
        self.check("a.sh", (
            "#!/bin/sh\n"
            "# gone\n"
            "echo \"# kept\" '# kept' a#b $# ${#x} \"$(echo \"#\")\" # gone\n"
            "# shellcheck disable=SC2086\n"
            "cat <<'EOF' # gone\n"
            "# kept in the here-document\n"
            "EOF\n"
            "cat <<-END\n"
            "\t# kept\n"
            "\tEND\n"
            "x=$(echo hi) # gone\n"
        ), (
            "#!/bin/sh\n"
            "echo \"# kept\" '# kept' a#b $# ${#x} \"$(echo \"#\")\"\n"
            "# shellcheck disable=SC2086\n"
            "cat <<'EOF'\n"
            "# kept in the here-document\n"
            "EOF\n"
            "cat <<-END\n"
            "\t# kept\n"
            "\tEND\n"
            "x=$(echo hi)\n"
        ))

    def test_csharp_raw_holes(self):
        self.check("A.cs", (
            "var a = $\"\"\"{1 /* gone */} // kept\"\"\";\n"
            "var b = $$\"\"\"{kept /* kept */} {{F(/* gone */2)}}\"\"\";\n"
        ), (
            "var a = $\"\"\"{1 } // kept\"\"\";\n"
            "var b = $$\"\"\"{kept /* kept */} {{F(2)}}\"\"\";\n"
        ))

    def test_tokens_stay_apart(self):
        self.check("a.cpp", "int f(){ return/*why*/0; }\nint g = h(/*x*/1) -/**/-2 /* a */ + 3;\n",
                   "int f(){ return 0; }\nint g = h(1) - -2 + 3;\n")
        self.check("Main.qml", "console.log(typeof/* x */missing);\n", "console.log(typeof missing);\n")

    def test_shell_heredoc_delimiters(self):
        self.check("a.sh", (
            "cat <<'END-TEXT'\n"
            "# kept\n"
            "END-TEXT\n"
            "cat <<123 # gone\n"
            "# kept\n"
            "123\n"
            "cat <<\\EOF\n"
            "# kept\n"
            "EOF\n"
            "echo $((1<<2)) \"$((3<<1))\" # gone\n"
            "# gone\n"
        ), (
            "cat <<'END-TEXT'\n"
            "# kept\n"
            "END-TEXT\n"
            "cat <<123\n"
            "# kept\n"
            "123\n"
            "cat <<\\EOF\n"
            "# kept\n"
            "EOF\n"
            "echo $((1<<2)) \"$((3<<1))\"\n"
        ))

    def test_csharp_format_specifiers(self):
        self.check("A.cs", (
            "var a = $\"{12:0 /* kept */}\" + $\"\"\"{42:0//0}\"\"\"; // gone\n"
            "var b = $\"{(x ? 1 : 2) /* gone */,5:N0}{global::System.Math.PI}\";\n"
        ), (
            "var a = $\"{12:0 /* kept */}\" + $\"\"\"{42:0//0}\"\"\";\n"
            "var b = $\"{(x ? 1 : 2) ,5:N0}{global::System.Math.PI}\";\n"
        ))

    def test_xml_text_stays_joined(self):
        self.check("App.csproj", "<Name>foo<!-- why -->bar</Name>\n", "<Name>foobar</Name>\n")

    def test_continued_lines_stay_apart(self):
        self.check("a.sh", "printf '%s\\n' foo \\\n# gone\ntrue\n", "printf '%s\\n' foo \\\n\ntrue\n")

    def test_shell_arithmetic_with_commands(self):
        self.check("a.sh", (
            "echo $(( $(printf %s 1 \"(\" | cut -c1) + 1 )) # gone\n"
            "# gone\n"
            "echo '# kept'\n"
        ), (
            "echo $(( $(printf %s 1 \"(\" | cut -c1) + 1 ))\n"
            "echo '# kept'\n"
        ))

    def test_shell_expansions(self):
        self.check("a.sh", (
            "echo $'it\\'s # kept' ${x:- #kept} ${y#\"}\"} \"${z:-#}\" # gone\n"
        ), (
            "echo $'it\\'s # kept' ${x:- #kept} ${y#\"}\"} \"${z:-#}\"\n"
        ))

    def test_shell_literal_brace_in_expansion(self):
        self.check("a.sh", "echo ${x:-{} ${y:-${z}} # gone\n# gone\necho '# kept'\n", "echo ${x:-{} ${y:-${z}}\necho '# kept'\n")

    def test_cpp_continued_line_comment(self):
        self.check("a.cpp", "int f() {\n    // gone \\\n    return 1;\n    return 0;\n}\n", "int f() {\n    return 0;\n}\n")

    def test_regex_after_condition(self):
        self.check("Main.qml", (
            "if (ok) /[/*]/.test(s); // gone\n"
            "var r = f(a) / 2 /* gone */\n"
        ), (
            "if (ok) /[/*]/.test(s);\n"
            "var r = f(a) / 2\n"
        ))

    def test_shell_quoted_default_in_double_quotes(self):
        self.check("a.sh", "printf '%s\\n' \"${x:-\"a # b\"}\" # gone\n", "printf '%s\\n' \"${x:-\"a # b\"}\"\n")

    def test_csharp_string_after_keyword(self):
        self.check("A.cs", "string F() { return\"/* data */\"; } // gone\n", "string F() { return\"/* data */\"; }\n")

    def test_keyword_named_method_is_not_a_condition(self):
        self.check("Main.qml", "const n = obj.with(value) / scale; // gone\n", "const n = obj.with(value) / scale;\n")

    def test_shell_by_shebang(self):
        self.check("pre-commit", "#!/bin/sh\n# gone\nexec true\n", "#!/bin/sh\nexec true\n")

    def test_python(self):
        self.check("a.py", (
            "#!/usr/bin/env python3\n"
            "s = \"# kept\"  # gone\n"
            "import x  # noqa: F401\n"
        ), (
            "#!/usr/bin/env python3\n"
            "s = \"# kept\"\n"
            "import x  # noqa: F401\n"
        ))

    def test_msbuild(self):
        self.check("App.csproj", (
            "<Project>\n"
            "  <!-- gone\n"
            "       over two lines -->\n"
            "  <!--#if (kind == \"panel\")-->\n"
            "  <X><![CDATA[<!-- kept -->]]></X> <!-- gone -->\n"
            "</Project>\n"
        ), (
            "<Project>\n"
            "  <!--#if (kind == \"panel\")-->\n"
            "  <X><![CDATA[<!-- kept -->]]></X>\n"
            "</Project>\n"
        ))

    def test_blank_lines(self):
        self.check("A.cs", (
            "// header\n"
            "\n"
            "using X;\n"
            "\n"
            "// between\n"
            "\n"
            "class A { }\n"
            "\n"
            "// trailing\n"
        ), (
            "using X;\n"
            "\n"
            "class A { }\n"
        ))

    def test_untouched(self):
        for name, src in [
            ("G.cs", "// <auto-generated/>\n// generated, left alone\nclass G { }\n"),
            ("data.json", "{\"a\": \"// x\"} // x\n"),
            ("README.md", "# Title\n"),
        ]:
            self.assertEqual(comments.comments(name, src), [], name)


class Cli(unittest.TestCase):
    def test_check_then_fix(self):
        with tempfile.TemporaryDirectory() as d:
            path = os.path.join(d, "Backend.cs")
            with open(path, "w") as f:
                f.write("class B\n{\n    int x; // new\n}\n")
            out, err = io.StringIO(), io.StringIO()
            with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
                self.assertEqual(comments.main([path]), 1)
            self.assertEqual(out.getvalue(), f"{path}:3: // new\n")
            self.assertIn("--fix", err.getvalue())
            self.assertEqual(comments.main(["--fix", path]), 0)
            with open(path) as f:
                self.assertEqual(f.read(), "class B\n{\n    int x;\n}\n")
            self.assertEqual(comments.main([path]), 0)


if __name__ == "__main__":
    unittest.main()
