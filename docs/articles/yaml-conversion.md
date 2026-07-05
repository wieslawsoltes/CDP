---
title: YAML Test Recording Conversion
---

# YAML Test Recording Conversion

The CDP Inspector features a unified conversion layer that allows teams to parse a Test Studio YAML flow (`.flow.yaml`) and convert it seamlessly into target script formats (such as Playwright, Puppeteer, or Selenium C#). 

By translating parsed flow steps (`TestStudioStep`) into standard recorded actions (`RecordedStep`), you can visually edit test flows in Test Studio and instantly export them as executable scripts matching your team's automation framework.

---

## 1. Programmatic Conversion API

The conversion is handled by the `TestStudioStepConverter` helper class located in the `Chrome.DevTools.Protocol` package. 

### C# Example: Generating Scripts from a YAML Flow

You can load a YAML flow file, convert it to intermediate `RecordedStep` structures, and run any of the code generators:

```csharp
using System;
using System.IO;
using System.Collections.Generic;
using Chrome.DevTools.Protocol;

class Program
{
    static void Main()
    {
        // 1. Read the YAML flow definition
        string yamlContent = File.ReadAllText("login.flow.yaml");

        // 2. Parse and map to standard RecordedSteps
        List<RecordedStep> recordedSteps = TestStudioStepConverter.ConvertYamlToRecordedSteps(yamlContent);

        // 3. Instantiate your desired script generator
        string hostAddress = "localhost:9222";

        // Generate Playwright Script
        var playwrightGen = new PlaywrightGenerator();
        string playwrightCode = playwrightGen.Generate(recordedSteps, hostAddress);
        File.WriteAllText("tests/login.spec.js", playwrightCode);

        // Generate Puppeteer Script
        var puppeteerGen = new PuppeteerGenerator();
        string puppeteerCode = puppeteerGen.Generate(recordedSteps, hostAddress);
        File.WriteAllText("tests/login.puppeteer.js", puppeteerCode);

        // Generate Selenium C# Test
        var seleniumGen = new SeleniumCSharpGenerator();
        string seleniumCode = seleniumGen.Generate(recordedSteps, hostAddress);
        File.WriteAllText("tests/SeleniumLoginTests.cs", seleniumCode);

        Console.WriteLine("Scripts generated successfully!");
    }
}
```

---

## 2. Step Mapping Reference

The conversion layer translates the parsed YAML step format to the common code generator format as follows:

| YAML Action | Recorded Step Type | Parameters Extracted |
|-------------|--------------------|----------------------|
| `tap` / `tapOn` | `tap` | `selector`, `modifiers`, `clickCount`, `offsetX`, `offsetY` |
| `doubleTap` / `doubleTapOn` | `doubleTap` | `selector`, `offsetX`, `offsetY` |
| `longPress` / `longPressOn` | `longPress` | `selector`, `offsetX`, `offsetY` |
| `back` | `back` | *None* |
| `clearText` / `clear` | `clear` | `selector` |
| `delay` | `delay` | `value` (delay duration in milliseconds) |
| `inputText` / `input` | `inputText` | `selector` (optional), `value` / `text` |
| `pressKey` | `pressKey` | `key`, `modifiers` |
| *Other actions* | *Matches Action Name* | Maps standard coordinates, dimensions, URL, buttons, and target properties. |

### Selector-less Inputs (Focused Typing)
If a YAML step defines an `inputText` action without a selector (e.g. typing into a focused field), the converter maps it with an empty `Selector`. Code generators will automatically generate direct keyboard input calls:
* **Playwright**: `await page.keyboard.type(value);`
* **Puppeteer**: `await page.keyboard.type(value);`
* **Selenium C#**: `_actions.SendKeys(value).Perform();`

---

## 3. Useful Links & Related Documentation

* [YAML Test Format Specifications](/articles/yaml-test-format) — Flow schema definition
* [Code Generation Guides](/articles/code-generation) — Output file details and examples
* [Playwright E2E Integration](/articles/playwright) — Running Playwright tests on port 9222
* [Puppeteer E2E Integration](/articles/puppeteer) — Controlling Avalonia with Puppeteer
* [Selenium E2E Integration](/articles/selenium) — Running WebDriver tests under ChromeDriver
