namespace CdpInspectorApp.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CdpInspectorApp.Models;
using CdpInspectorApp.ViewModels;

public class PlaywrightGenerator : ICodeGenerator
{
    public RecordingFormat Format => RecordingFormat.PlaywrightTest;
    public string TabHeader => "Playwright Script";
    public string TitleText => "Generated Playwright Test Script";
    public string ExportButtonText => "Export Playwright";

    public string Generate(IEnumerable<RecordedStepModel> steps, string hostAddress)
    {
        var stepsList = steps.ToList();
        var sb = new StringBuilder();
        sb.AppendLine("import { test, expect, chromium } from '@playwright/test';");
        sb.AppendLine();
        sb.AppendLine("test.describe('CDP Recorded Tests', () => {");
        sb.AppendLine("  test('recorded test', async () => {");

        string host = hostAddress;
        if (string.IsNullOrEmpty(host))
        {
            host = "http://localhost:9222";
        }
        if (!host.StartsWith("http://") && !host.StartsWith("https://"))
        {
            host = "http://" + host;
        }
        if (host.EndsWith("/"))
        {
            host = host.Substring(0, host.Length - 1);
        }

        sb.AppendLine($"    const browser = await chromium.connectOverCDP('{EscapeJsString(host)}');");
        sb.AppendLine("    const context = browser.contexts()[0];");
        sb.AppendLine("    const page = context.pages()[0];");
        sb.AppendLine();

        bool hasViewportStep = stepsList.Any(s => s.Type == "setViewport");
        bool hasNavigateStep = stepsList.Any(s => s.Type == "navigate");

        if (!hasViewportStep)
        {
            sb.AppendLine("    await test.step('Set viewport size', async () => {");
            sb.AppendLine("      await page.setViewportSize({ width: 800, height: 600 });");
            sb.AppendLine("    });");
        }
        if (!hasNavigateStep)
        {
            sb.AppendLine("    await test.step('Navigate to application', async () => {");
            sb.AppendLine($"      await page.goto('{EscapeJsString(host)}/');");
            sb.AppendLine("    });");
        }

        for (int i = 0; i < stepsList.Count; i++)
        {
            var step = stepsList[i];
            sb.AppendLine();
            if (step.Type == "click")
            {
                var options = new List<string>();
                if (step.Button != "left") options.Add($"button: '{step.Button}'");
                if (step.ClickCount > 1) options.Add($"clickCount: {step.ClickCount}");
                if (step.Modifiers > 0)
                {
                    var modsList = string.Join(", ", GetModifiersList(step.Modifiers).Select(m => $"'{m}'"));
                    options.Add($"modifiers: [{modsList}]");
                }
                string optStr = options.Count > 0 ? $"{{ {string.Join(", ", options)} }}" : "";

                sb.AppendLine($"    await test.step('Click on element {EscapeJsString(step.Selector)}', async () => {{");
                sb.AppendLine($"      const element_{i} = page.locator('{EscapeJsString(step.Selector)}');");
                sb.AppendLine($"      await element_{i}.click({optStr});");
                sb.AppendLine("    });");
            }
            else if (step.Type == "change")
            {
                sb.AppendLine($"    await test.step('Type text in element {EscapeJsString(step.Selector)}', async () => {{");
                sb.AppendLine($"      const element_{i} = page.locator('{EscapeJsString(step.Selector)}');");
                sb.AppendLine($"      await element_{i}.fill('{EscapeJsString(step.Value)}');");
                sb.AppendLine("    });");
            }
            else if (step.Type == "setViewport")
            {
                sb.AppendLine($"    await test.step('Set viewport size', async () => {{");
                sb.AppendLine($"      await page.setViewportSize({{ width: {step.Width}, height: {step.Height} }});");
                sb.AppendLine("    });");
            }
            else if (step.Type == "navigate")
            {
                sb.AppendLine($"    await test.step('Navigate to {EscapeJsString(step.Url)}', async () => {{");
                sb.AppendLine($"      await page.goto('{EscapeJsString(step.Url)}');");
                sb.AppendLine("    });");
            }
            else if (step.Type == "keydown")
            {
                sb.AppendLine($"    await test.step('Press key {EscapeJsString(step.Key)}', async () => {{");
                if (step.Modifiers > 0)
                {
                    foreach (var mod in GetModifiersList(step.Modifiers)) sb.AppendLine($"      await page.keyboard.down('{mod}');");
                }
                sb.AppendLine($"      await page.keyboard.press('{EscapeJsString(step.Key)}');");
                if (step.Modifiers > 0)
                {
                    foreach (var mod in GetModifiersList(step.Modifiers)) sb.AppendLine($"      await page.keyboard.up('{mod}');");
                }
                sb.AppendLine("    });");
            }
            else if (step.Type == "dragAndDrop")
            {
                sb.AppendLine($"    await test.step('Drag element {EscapeJsString(step.Selector)} to {EscapeJsString(step.TargetSelector)}', async () => {{");
                sb.AppendLine($"      const source_{i} = page.locator('{EscapeJsString(step.Selector)}');");
                sb.AppendLine($"      const target_{i} = page.locator('{EscapeJsString(step.TargetSelector)}');");
                if (step.Modifiers > 0)
                {
                    foreach (var mod in GetModifiersList(step.Modifiers)) sb.AppendLine($"      await page.keyboard.down('{mod}');");
                }
                sb.AppendLine($"      await source_{i}.dragTo(target_{i});");
                if (step.Modifiers > 0)
                {
                    foreach (var mod in GetModifiersList(step.Modifiers)) sb.AppendLine($"      await page.keyboard.up('{mod}');");
                }
                sb.AppendLine("    });");
            }
            else if (step.Type == "scroll")
            {
                sb.AppendLine($"    await test.step('Scroll element or page', async () => {{");
                if (!string.IsNullOrEmpty(step.Selector))
                {
                    sb.AppendLine($"      const element_{i} = page.locator('{EscapeJsString(step.Selector)}');");
                    sb.AppendLine($"      await element_{i}.evaluate(el => el.scrollBy({-step.OffsetX}, {-step.OffsetY}));");
                }
                else
                {
                    sb.AppendLine($"      await page.evaluate(() => window.scrollBy({-step.OffsetX}, {-step.OffsetY}));");
                }
                sb.AppendLine("    });");
            }
            else if (step.Type == "assertVisible")
            {
                sb.AppendLine($"    await test.step('Assert element {EscapeJsString(step.Selector)} is visible', async () => {{");
                sb.AppendLine($"      await expect(page.locator('{EscapeJsString(step.Selector)}')).toBeVisible();");
                sb.AppendLine("    });");
            }
            else if (step.Type == "assertNotVisible")
            {
                sb.AppendLine($"    await test.step('Assert element {EscapeJsString(step.Selector)} is hidden', async () => {{");
                sb.AppendLine($"      await expect(page.locator('{EscapeJsString(step.Selector)}')).toBeHidden();");
                sb.AppendLine("    });");
            }
        }

        sb.AppendLine();
        sb.AppendLine("    await browser.close();");
        sb.AppendLine("  });");
        sb.AppendLine("});");

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
