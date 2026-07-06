using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Chrome.DevTools.Protocol;

public class AppiumCSharpGenerator : ICodeGenerator
{
    public RecordingFormat Format => RecordingFormat.AppiumCSharp;
    public string TabHeader => "Appium C#";
    public string TitleText => "Generated Appium C# Script";
    public string ExportButtonText => "Export Appium C#";

    public string Generate(IEnumerable<RecordedStep> steps, string hostAddress)
    {
        var stepsList = steps.ToList();
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Drawing;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using OpenQA.Selenium;");
        sb.AppendLine("using OpenQA.Selenium.Appium;");
        sb.AppendLine("using OpenQA.Selenium.Appium.Android;");
        sb.AppendLine("using OpenQA.Selenium.Interactions;");
        sb.AppendLine("using Xunit;");
        sb.AppendLine("using CDP.Automation.Appium;");
        sb.AppendLine();
        sb.AppendLine("namespace AppiumTests");
        sb.AppendLine("{");
        sb.AppendLine("    public class RecordedTests : IClassFixture<CdpAppiumFixture>");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly CdpAppiumFixture _fixture;");
        sb.AppendLine("        private readonly AndroidDriver _driver;");
        sb.AppendLine();
        sb.AppendLine("        public RecordedTests(CdpAppiumFixture fixture)");
        sb.AppendLine("        {");
        sb.AppendLine("            _fixture = fixture;");
        sb.AppendLine("            _driver = fixture.Driver ?? throw new InvalidOperationException(\"Driver was not initialized.\");");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        [Fact]");
        sb.AppendLine("        public void TestRecordedSteps()");
        sb.AppendLine("        {");

        for (int i = 0; i < stepsList.Count; i++)
        {
            var step = stepsList[i];
            sb.AppendLine($"            // Step {i + 1}: {step.Type}");

            if (step.Type == "setViewport")
            {
                sb.AppendLine($"            _driver.Manage().Window.Size = new Size({(int)step.Width}, {(int)step.Height});");
            }
            else if (step.Type == "navigate")
            {
                sb.AppendLine($"            _driver.Navigate().GoToUrl(\"{EscapeCSharpString(step.Url)}\");");
            }
            else if (step.Type == "click")
            {
                string elementExpr = GetAppiumElementExpression(step.Selector);
                if (step.Button == "right")
                {
                    sb.AppendLine($"            new Actions(_driver).ContextClick({elementExpr}).Perform();");
                }
                else if (step.Button == "middle")
                {
                    sb.AppendLine($"            new Actions(_driver).Click({elementExpr}).Perform();");
                }
                else if (step.ClickCount == 2)
                {
                    sb.AppendLine($"            new Actions(_driver).DoubleClick({elementExpr}).Perform();");
                }
                else if (step.ClickCount > 2)
                {
                    sb.AppendLine($"            var element_{i} = {elementExpr};");
                    sb.AppendLine($"            for (int c_{i} = 0; c_{i} < {step.ClickCount}; c_{i}++)");
                    sb.AppendLine($"            {{");
                    sb.AppendLine($"                element_{i}.Click();");
                    sb.AppendLine($"            }}");
                }
                else if (step.Modifiers > 0)
                {
                    var mods = GetSeleniumModifiersList(step.Modifiers);
                    sb.AppendLine($"            var element_{i} = {elementExpr};");
                    sb.AppendLine($"            var action_{i} = new Actions(_driver).MoveToElement(element_{i});");
                    foreach (var mod in mods)
                    {
                        sb.AppendLine($"            action_{i} = action_{i}.KeyDown({mod});");
                    }
                    sb.AppendLine($"            action_{i} = action_{i}.Click();");
                    foreach (var mod in mods)
                    {
                        sb.AppendLine($"            action_{i} = action_{i}.KeyUp({mod});");
                    }
                    sb.AppendLine($"            action_{i}.Perform();");
                }
                else
                {
                    sb.AppendLine($"            {elementExpr}.Click();");
                }
            }
            else if (step.Type == "change")
            {
                string elementExpr = GetAppiumElementExpression(step.Selector);
                string valueEscaped = EscapeCSharpString(step.Value);
                sb.AppendLine($"            var element_{i} = {elementExpr};");
                sb.AppendLine($"            element_{i}.Clear();");
                sb.AppendLine($"            element_{i}.SendKeys(\"{valueEscaped}\");");
            }
            else if (step.Type == "keydown")
            {
                var keyRep = GetSeleniumKeyRepresentation(step.Key, out bool isSpecialKey);
                if (step.Modifiers > 0)
                {
                    var mods = GetSeleniumModifiersList(step.Modifiers);
                    sb.AppendLine($"            var action_{i} = new Actions(_driver);");
                    foreach (var mod in mods)
                    {
                        sb.AppendLine($"            action_{i} = action_{i}.KeyDown({mod});");
                    }
                    sb.AppendLine($"            action_{i} = action_{i}.SendKeys({keyRep});");
                    foreach (var mod in mods)
                    {
                        sb.AppendLine($"            action_{i} = action_{i}.KeyUp({mod});");
                    }
                    sb.AppendLine($"            action_{i}.Perform();");
                }
                else
                {
                    sb.AppendLine($"            new Actions(_driver).SendKeys({keyRep}).Perform();");
                }
            }
            else if (step.Type == "dragAndDrop")
            {
                string sourceExpr = GetAppiumElementExpression(step.Selector);
                string targetExpr = GetAppiumElementExpression(step.TargetSelector);
                sb.AppendLine($"            var source_{i} = {sourceExpr};");
                sb.AppendLine($"            var target_{i} = {targetExpr};");
                sb.AppendLine($"            new Actions(_driver).DragAndDrop(source_{i}, target_{i}).Perform();");
            }
            else if (step.Type == "scroll")
            {
                sb.AppendLine($"            // Scroll element or page");
                if (!string.IsNullOrEmpty(step.Selector))
                {
                    string elementExpr = GetAppiumElementExpression(step.Selector);
                    sb.AppendLine($"            var element_{i} = {elementExpr};");
                    sb.AppendLine($"            ((IJavaScriptExecutor)_driver).ExecuteScript(\"var el = arguments[0]; while (el) {{ if (el.scrollHeight > el.clientHeight && window.getComputedStyle(el).overflowY !== 'visible') {{ el.scrollBy({-step.OffsetX}, {-step.OffsetY}); return; }} el = el.parentElement; }} window.scrollBy({-step.OffsetX}, {-step.OffsetY});\", element_{i});");
                }
                else
                {
                    sb.AppendLine($"            ((IJavaScriptExecutor)_driver).ExecuteScript(\"window.scrollBy({-step.OffsetX}, {-step.OffsetY});\");");
                }
            }
            else if (step.Type == "assertVisible")
            {
                string elementExpr = GetAppiumElementExpression(step.Selector);
                sb.AppendLine($"            Assert.True({elementExpr}.Displayed);");
            }
            else if (step.Type == "assertNotVisible")
            {
                string elementExpr = GetAppiumElementExpression(step.Selector);
                sb.AppendLine($"            bool isVisible_{i} = false;");
                sb.AppendLine("            try");
                sb.AppendLine("            {");
                sb.AppendLine($"                isVisible_{i} = {elementExpr}.Displayed;");
                sb.AppendLine("            }");
                sb.AppendLine("            catch (Exception)");
                sb.AppendLine("            {");
                sb.AppendLine($"                isVisible_{i} = false;");
                sb.AppendLine("            }");
                sb.AppendLine($"            Assert.False(isVisible_{i});");
            }
            else if (step.Type == "assertTrue" || step.Type == "assertFalse")
            {
                bool expectedBool = step.Type == "assertTrue";
                string expr = EscapeCSharpString(step.Value);
                sb.AppendLine($"            var result_{i} = ((IJavaScriptExecutor)_driver).ExecuteScript(\"return {expr};\");");
                if (expectedBool)
                {
                    sb.AppendLine($"            Assert.True(Convert.ToBoolean(result_{i}));");
                }
                else
                {
                    sb.AppendLine($"            Assert.False(Convert.ToBoolean(result_{i}));");
                }
            }
            else
            {
                sb.AppendLine($"            // Warning: Unsupported step type '{step.Type}'");
            }
            sb.AppendLine();
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GetAppiumElementExpression(string selector)
    {
        if (selector.StartsWith("[AccessibilityId=\"") && selector.EndsWith("\"]"))
        {
            var value = selector.Substring("[AccessibilityId=\"".Length, selector.Length - "[AccessibilityId=\"".Length - "\"]".Length);
            return $"_driver.FindElement(MobileBy.AccessibilityId(\"{EscapeCSharpString(value)}\"))";
        }
        else if (selector.Contains("[AccessibilityId=\""))
        {
            int start = selector.IndexOf("[AccessibilityId=\"") + "[AccessibilityId=\"".Length;
            int end = selector.IndexOf("\"]", start);
            if (end > start)
            {
                var value = selector.Substring(start, end - start);
                return $"_driver.FindElement(MobileBy.AccessibilityId(\"{EscapeCSharpString(value)}\"))";
            }
        }

        string escaped = EscapeCSharpString(selector);
        if (selector.StartsWith("#"))
        {
            return $"_driver.FindElement(By.Id(\"{EscapeCSharpString(selector.Substring(1))}\"))";
        }
        else if (selector.StartsWith("/") || selector.StartsWith("//"))
        {
            return $"_driver.FindElement(By.XPath(\"{escaped}\"))";
        }
        else
        {
            return $"_driver.FindElement(By.Name(\"{escaped}\"))";
        }
    }

    private static List<string> GetSeleniumModifiersList(int modifiers)
    {
        var list = new List<string>();
        if ((modifiers & 1) != 0) list.Add("Keys.Alt");
        if ((modifiers & 2) != 0) list.Add("Keys.Control");
        if ((modifiers & 4) != 0) list.Add("Keys.Command");
        if ((modifiers & 8) != 0) list.Add("Keys.Shift");
        return list;
    }

    private static string GetSeleniumKeyRepresentation(string key, out bool isSpecialKey)
    {
        switch (key)
        {
            case "Enter": isSpecialKey = true; return "Keys.Enter";
            case "Tab": isSpecialKey = true; return "Keys.Tab";
            case "Escape": isSpecialKey = true; return "Keys.Escape";
            case "Backspace": isSpecialKey = true; return "Keys.Backspace";
            case "Delete": isSpecialKey = true; return "Keys.Delete";
            case "ArrowUp": isSpecialKey = true; return "Keys.Up";
            case "ArrowDown": isSpecialKey = true; return "Keys.Down";
            case "ArrowLeft": isSpecialKey = true; return "Keys.Left";
            case "ArrowRight": isSpecialKey = true; return "Keys.Right";
            case "PageUp": isSpecialKey = true; return "Keys.PageUp";
            case "PageDown": isSpecialKey = true; return "Keys.PageDown";
            case "Home": isSpecialKey = true; return "Keys.Home";
            case "End": isSpecialKey = true; return "Keys.End";
            default: isSpecialKey = false; return $"\"{EscapeCSharpString(key)}\"";
        }
    }

    private static string EscapeCSharpString(string? value)
    {
        if (value == null) return "";
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
