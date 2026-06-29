using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Chrome.DevTools.Protocol;

public class PuppeteerGenerator : ICodeGenerator
{
    public RecordingFormat Format => RecordingFormat.Puppeteer;
    public string TabHeader => "Puppeteer Script";
    public string TitleText => "Generated Puppeteer Script";
    public string ExportButtonText => "Export Puppeteer";

    public string Generate(IEnumerable<RecordedStep> steps, string hostAddress)
    {
        var stepsList = steps.ToList();
        var sb = new StringBuilder();
        sb.AppendLine("const puppeteer = require('puppeteer');");
        sb.AppendLine();
        sb.AppendLine("(async () => {");
        sb.AppendLine("  const browser = await puppeteer.launch({ headless: false });");
        sb.AppendLine("  const page = await browser.newPage();");

        bool hasViewportStep = stepsList.Any(s => s.Type == "setViewport");
        bool hasNavigateStep = stepsList.Any(s => s.Type == "navigate");

        if (!hasViewportStep)
        {
            sb.AppendLine("  await page.setViewport({ width: 800, height: 600 });");
        }
        if (!hasNavigateStep)
        {
            string host = hostAddress;
            if (string.IsNullOrEmpty(host))
            {
                host = "http://localhost:9222";
            }
            if (!host.StartsWith("http://") && !host.StartsWith("https://"))
            {
                host = "http://" + host;
            }
            if (!host.EndsWith("/"))
            {
                host += "/";
            }
            sb.AppendLine($"  await page.goto('{host}');");
        }
        sb.AppendLine();

        for (int i = 0; i < stepsList.Count; i++)
        {
            var step = stepsList[i];
            if (step.Type == "click")
            {
                var options = new List<string>();
                if (step.Button != "left") options.Add($"button: '{step.Button}'");
                if (step.ClickCount > 1) options.Add($"clickCount: {step.ClickCount}");
                string optStr = options.Count > 0 ? $"{{ {string.Join(", ", options)} }}" : "";

                sb.AppendLine($"  // Click on element");
                if (step.Modifiers > 0)
                {
                    foreach (var mod in GetModifiersList(step.Modifiers)) sb.AppendLine($"  await page.keyboard.down('{mod}');");
                }
                sb.AppendLine($"  const element_{i} = await page.waitForSelector('{EscapeJsString(step.Selector)}');");
                sb.AppendLine($"  await element_{i}.click({optStr});");
                if (step.Modifiers > 0)
                {
                    foreach (var mod in GetModifiersList(step.Modifiers)) sb.AppendLine($"  await page.keyboard.up('{mod}');");
                }
            }
            else if (step.Type == "change")
            {
                sb.AppendLine($"  // Type text in element");
                sb.AppendLine($"  const element_{i} = await page.waitForSelector('{EscapeJsString(step.Selector)}');");
                sb.AppendLine($"  await element_{i}.type('{EscapeJsString(step.Value)}');");
            }
            else if (step.Type == "setViewport")
            {
                sb.AppendLine($"  await page.setViewport({{ width: {step.Width}, height: {step.Height} }});");
            }
            else if (step.Type == "navigate")
            {
                sb.AppendLine($"  await page.goto('{EscapeJsString(step.Url)}');");
            }
            else if (step.Type == "keydown")
            {
                if (step.Modifiers > 0)
                {
                    foreach (var mod in GetModifiersList(step.Modifiers)) sb.AppendLine($"  await page.keyboard.down('{mod}');");
                }
                sb.AppendLine($"  await page.keyboard.press('{EscapeJsString(step.Key)}');");
                if (step.Modifiers > 0)
                {
                    foreach (var mod in GetModifiersList(step.Modifiers)) sb.AppendLine($"  await page.keyboard.up('{mod}');");
                }
            }
            else if (step.Type == "dragAndDrop")
            {
                sb.AppendLine($"  // Drag and drop");
                sb.AppendLine($"  const source_{i} = await page.waitForSelector('{EscapeJsString(step.Selector)}');");
                sb.AppendLine($"  const target_{i} = await page.waitForSelector('{EscapeJsString(step.TargetSelector)}');");
                if (step.Modifiers > 0)
                {
                    foreach (var mod in GetModifiersList(step.Modifiers)) sb.AppendLine($"  await page.keyboard.down('{mod}');");
                }
                sb.AppendLine($"  await source_{i}.dragTo(target_{i});");
                if (step.Modifiers > 0)
                {
                    foreach (var mod in GetModifiersList(step.Modifiers)) sb.AppendLine($"  await page.keyboard.up('{mod}');");
                }
            }
            else if (step.Type == "scroll")
            {
                sb.AppendLine($"  // Scroll element or page");
                if (!string.IsNullOrEmpty(step.Selector))
                {
                    sb.AppendLine($"  const element_{i} = await page.waitForSelector('{EscapeJsString(step.Selector)}');");
                    sb.AppendLine($"  await element_{i}.evaluate(el => {{");
                    sb.AppendLine($"    let parent = el;");
                    sb.AppendLine($"    while (parent) {{");
                    sb.AppendLine($"      if (parent.scrollHeight > parent.clientHeight && window.getComputedStyle(parent).overflowY !== 'visible') {{");
                    sb.AppendLine($"        parent.scrollBy({-step.OffsetX}, {-step.OffsetY});");
                    sb.AppendLine($"        return;");
                    sb.AppendLine($"      }}");
                    sb.AppendLine($"      parent = parent.parentElement;");
                    sb.AppendLine($"    }}");
                    sb.AppendLine($"    window.scrollBy({-step.OffsetX}, {-step.OffsetY});");
                    sb.AppendLine($"  }});");
                }
                else
                {
                    sb.AppendLine($"  await page.evaluate(() => window.scrollBy({-step.OffsetX}, {-step.OffsetY}));");
                }
            }
            else if (step.Type == "assertVisible")
            {
                sb.AppendLine($"  // Assert element is visible");
                sb.AppendLine($"  await page.waitForSelector('{EscapeJsString(step.Selector)}', {{ visible: true }});");
            }
            else if (step.Type == "assertNotVisible")
            {
                sb.AppendLine($"  // Assert element is hidden");
                sb.AppendLine($"  await page.waitForSelector('{EscapeJsString(step.Selector)}', {{ hidden: true }});");
            }
            else if (step.Type == "assertTrue")
            {
                sb.AppendLine($"  // Assert True");
                sb.AppendLine($"  const result_{i} = await page.evaluate('{EscapeJsString(step.Value)}');");
                sb.AppendLine($"  if (!result_{i}) throw new Error('Assertion failed: {EscapeJsString(step.Value)} is not true');");
            }
            else if (step.Type == "assertFalse")
            {
                sb.AppendLine($"  // Assert False");
                sb.AppendLine($"  const result_{i} = await page.evaluate('{EscapeJsString(step.Value)}');");
                sb.AppendLine($"  if (result_{i}) throw new Error('Assertion failed: {EscapeJsString(step.Value)} is not false');");
            }
            sb.AppendLine();
        }

        sb.AppendLine("  await browser.close();");
        sb.AppendLine("})();");

        return sb.ToString();
    }

    private static List<string> GetModifiersList(int modifiers)
    {
        var list = new List<string>();
        if ((modifiers & 1) != 0) list.Add("Alt");
        if ((modifiers & 2) != 0) list.Add("Control");
        if ((modifiers & 4) != 0) list.Add("Meta");
        if ((modifiers & 8) != 0) list.Add("Shift");
        return list;
    }

    private static string EscapeJsString(string? value)
    {
        if (value == null) return "";
        return value
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
