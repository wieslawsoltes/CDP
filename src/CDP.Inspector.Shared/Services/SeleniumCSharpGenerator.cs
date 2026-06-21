using System;
using System.Collections.Generic;
using System.Text;
using CdpInspectorApp.Models;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Services;

public class SeleniumCSharpGenerator : ICodeGenerator
{
    public RecordingFormat Format => RecordingFormat.SeleniumCSharp;
    public string TabHeader => "Selenium C#";
    public string TitleText => "Generated Selenium C# Script";
    public string ExportButtonText => "Export Selenium C#";

    public string Generate(IEnumerable<RecordedStepModel> steps, string hostAddress)
    {
        var stepsList = steps.ToList();
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Drawing;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using NUnit.Framework;");
        sb.AppendLine("using OpenQA.Selenium;");
        sb.AppendLine("using OpenQA.Selenium.Chrome;");
        sb.AppendLine("using OpenQA.Selenium.Interactions;");
        sb.AppendLine();
        sb.AppendLine("namespace SeleniumTests");
        sb.AppendLine("{");
        sb.AppendLine("    [TestFixture]");
        sb.AppendLine("    public class RecordedTests");
        sb.AppendLine("    {");
        sb.AppendLine("        private IWebDriver _driver;");
        sb.AppendLine("        private Actions _actions;");
        sb.AppendLine();
        sb.AppendLine("        [SetUp]");
        sb.AppendLine("        public void SetUp()");
        sb.AppendLine("        {");
        sb.AppendLine("            var options = new ChromeOptions();");
        
        var cleanHost = hostAddress ?? "localhost:9222";
        if (cleanHost.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            cleanHost = cleanHost.Substring(7);
        }
        else if (cleanHost.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            cleanHost = cleanHost.Substring(8);
        }
        if (cleanHost.EndsWith("/"))
        {
            cleanHost = cleanHost.Substring(0, cleanHost.Length - 1);
        }
        
        sb.AppendLine($"            options.DebuggerAddress = \"{EscapeCSharpString(cleanHost)}\";");
        sb.AppendLine("            _driver = new ChromeDriver(options);");
        sb.AppendLine("            _actions = new Actions(_driver);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        [TearDown]");
        sb.AppendLine("        public void TearDown()");
        sb.AppendLine("        {");
        sb.AppendLine("            _driver?.Quit();");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        [Test]");
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
                string selectorEscaped = EscapeCSharpString(step.Selector);
                if (step.Button == "right")
                {
                    sb.AppendLine($"            _actions.ContextClick(_driver.FindElement(By.CssSelector(\"{selectorEscaped}\"))).Perform();");
                }
                else if (step.Button == "middle")
                {
                    sb.AppendLine($"            ((IJavaScriptExecutor)_driver).ExecuteScript(\"arguments[0].dispatchEvent(new MouseEvent('click', {{button: 1}}));\", _driver.FindElement(By.CssSelector(\"{selectorEscaped}\")));");
                }
                else if (step.ClickCount == 2)
                {
                    sb.AppendLine($"            _actions.DoubleClick(_driver.FindElement(By.CssSelector(\"{selectorEscaped}\"))).Perform();");
                }
                else if (step.ClickCount > 2)
                {
                    sb.AppendLine($"            var element_{i} = _driver.FindElement(By.CssSelector(\"{selectorEscaped}\"));");
                    sb.AppendLine($"            for (int c_{i} = 0; c_{i} < {step.ClickCount}; c_{i}++)");
                    sb.AppendLine($"            {{");
                    sb.AppendLine($"                element_{i}.Click();");
                    sb.AppendLine($"            }}");
                }
                else if (step.Modifiers > 0)
                {
                    var mods = GetSeleniumModifiersList(step.Modifiers);
                    sb.AppendLine($"            var element_{i} = _driver.FindElement(By.CssSelector(\"{selectorEscaped}\"));");
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
                    sb.AppendLine($"            _driver.FindElement(By.CssSelector(\"{selectorEscaped}\")).Click();");
                }
            }
            else if (step.Type == "change")
            {
                string selectorEscaped = EscapeCSharpString(step.Selector);
                string valueEscaped = EscapeCSharpString(step.Value);
                sb.AppendLine($"            var element_{i} = _driver.FindElement(By.CssSelector(\"{selectorEscaped}\"));");
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
                string selectorEscaped = EscapeCSharpString(step.Selector);
                string targetSelectorEscaped = EscapeCSharpString(step.TargetSelector);
                sb.AppendLine($"            var source_{i} = _driver.FindElement(By.CssSelector(\"{selectorEscaped}\"));");
                sb.AppendLine($"            var target_{i} = _driver.FindElement(By.CssSelector(\"{targetSelectorEscaped}\"));");
                sb.AppendLine($"            _actions.DragAndDrop(source_{i}, target_{i}).Perform();");
            }
            else if (step.Type == "scroll")
            {
                sb.AppendLine($"            // Scroll element or page");
                if (!string.IsNullOrEmpty(step.Selector))
                {
                    sb.AppendLine($"            var element_{i} = _driver.FindElement(By.CssSelector(\"{EscapeCSharpString(step.Selector)}\"));");
                    sb.AppendLine($"            ((IJavaScriptExecutor)_driver).ExecuteScript(\"var el = arguments[0]; while (el) {{ if (el.scrollHeight > el.clientHeight && window.getComputedStyle(el).overflowY !== 'visible') {{ el.scrollBy({-step.OffsetX}, {-step.OffsetY}); return; }} el = el.parentElement; }} window.scrollBy({-step.OffsetX}, {-step.OffsetY});\", element_{i});");
                }
                else
                {
                    sb.AppendLine($"            ((IJavaScriptExecutor)_driver).ExecuteScript(\"window.scrollBy({-step.OffsetX}, {-step.OffsetY});\");");
                }
            }
            else if (step.Type == "assertVisible")
            {
                string selectorEscaped = EscapeCSharpString(step.Selector);
                sb.AppendLine($"            Assert.IsTrue(_driver.FindElement(By.CssSelector(\"{selectorEscaped}\")).Displayed);");
            }
            else if (step.Type == "assertNotVisible")
            {
                string selectorEscaped = EscapeCSharpString(step.Selector);
                sb.AppendLine($"            bool isVisible_{i} = false;");
                sb.AppendLine("            try");
                sb.AppendLine("            {");
                sb.AppendLine($"                isVisible_{i} = _driver.FindElement(By.CssSelector(\"{selectorEscaped}\")).Displayed;");
                sb.AppendLine("            }");
                sb.AppendLine("            catch (NoSuchElementException)");
                sb.AppendLine("            {");
                sb.AppendLine($"                isVisible_{i} = false;");
                sb.AppendLine("            }");
                sb.AppendLine($"            Assert.IsFalse(isVisible_{i});");
            }
            sb.AppendLine();
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
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
