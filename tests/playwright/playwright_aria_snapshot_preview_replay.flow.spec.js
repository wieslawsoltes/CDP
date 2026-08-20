import { test, expect, chromium } from '@playwright/test';

test.describe('CDP Recorded Tests', () => {
  test('recorded test', async () => {
    const browser = await chromium.connectOverCDP('http://127.0.0.1:9223');
    const context = browser.contexts()[0];
    const page = context.pages()[0];

    await test.step('Set viewport size', async () => {
      await page.setViewportSize({ width: 800, height: 600 });
    });

    await test.step('Evaluate Script: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Connection.DisconnectCommand.Execute(null)', async () => {
      await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Connection.DisconnectCommand.Execute(null)');
    });

    await test.step('Delay 300ms', async () => {
      await page.waitForTimeout(300);
    });

    await test.step('Evaluate Script: var connection = ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Connection;\nconnection.HostAddress = "http://127.0.0.1:9222";\nconnection.RefreshTargetsCommand.Execute(null);\n', async () => {
      await page.evaluate('var connection = ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Connection;\nconnection.HostAddress = "http://127.0.0.1:9222";\nconnection.RefreshTargetsCommand.Execute(null);\n');
    });

    await test.step('Delay 700ms', async () => {
      await page.waitForTimeout(700);
    });

    await test.step('Evaluate Script: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Connection.ConnectCommand.Execute(null)', async () => {
      await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Connection.ConnectCommand.Execute(null)');
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Connection.IsConnected == true', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Connection.IsConnected == true');
      await expect(result).toBeTruthy();
    });

    await test.step('Evaluate Script: var vm = (CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext;\nvar evaluation = vm.CdpService.SendCommandAsync(\n    "Runtime.evaluate",\n    new System.Text.Json.Nodes.JsonObject\n    {\n        ["expression"] = "(() => { class UtilityScript {} return new UtilityScript(); })()"\n    }).GetAwaiter().GetResult();\nvar objectId = evaluation["result"]?["objectId"]?.GetValue<string>();\nif (string.IsNullOrWhiteSpace(objectId))\n{\n    throw new System.InvalidOperationException("Playwright utility object was not registered.");\n}\nvar call = vm.CdpService.SendCommandAsync(\n    "Runtime.callFunctionOn",\n    new System.Text.Json.Nodes.JsonObject\n    {\n        ["objectId"] = objectId,\n        ["functionDeclaration"] = "(utilityScript) => utilityScript.incrementalAriaSnapshot()",\n        ["returnByValue"] = true\n    }).GetAwaiter().GetResult();\nvar snapshot = call["result"]?["value"] as System.Text.Json.Nodes.JsonObject;\nif (snapshot?["iframeRefs"] is not System.Text.Json.Nodes.JsonArray iframeRefs || iframeRefs.Count != 0)\n{\n    throw new System.InvalidOperationException("Aria snapshot iframeRefs must be an empty array.");\n}\nif (snapshot["iframeDepths"] is not System.Text.Json.Nodes.JsonObject iframeDepths || iframeDepths.Count != 0)\n{\n    throw new System.InvalidOperationException("Aria snapshot iframeDepths must be an empty object.");\n}\nif (snapshot["full"] == null)\n{\n    throw new System.InvalidOperationException("Aria snapshot full text is missing.");\n}\n', async () => {
      await page.evaluate('var vm = (CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext;\nvar evaluation = vm.CdpService.SendCommandAsync(\n    "Runtime.evaluate",\n    new System.Text.Json.Nodes.JsonObject\n    {\n        ["expression"] = "(() => { class UtilityScript {} return new UtilityScript(); })()"\n    }).GetAwaiter().GetResult();\nvar objectId = evaluation["result"]?["objectId"]?.GetValue<string>();\nif (string.IsNullOrWhiteSpace(objectId))\n{\n    throw new System.InvalidOperationException("Playwright utility object was not registered.");\n}\nvar call = vm.CdpService.SendCommandAsync(\n    "Runtime.callFunctionOn",\n    new System.Text.Json.Nodes.JsonObject\n    {\n        ["objectId"] = objectId,\n        ["functionDeclaration"] = "(utilityScript) => utilityScript.incrementalAriaSnapshot()",\n        ["returnByValue"] = true\n    }).GetAwaiter().GetResult();\nvar snapshot = call["result"]?["value"] as System.Text.Json.Nodes.JsonObject;\nif (snapshot?["iframeRefs"] is not System.Text.Json.Nodes.JsonArray iframeRefs || iframeRefs.Count != 0)\n{\n    throw new System.InvalidOperationException("Aria snapshot iframeRefs must be an empty array.");\n}\nif (snapshot["iframeDepths"] is not System.Text.Json.Nodes.JsonObject iframeDepths || iframeDepths.Count != 0)\n{\n    throw new System.InvalidOperationException("Aria snapshot iframeDepths must be an empty object.");\n}\nif (snapshot["full"] == null)\n{\n    throw new System.InvalidOperationException("Aria snapshot full text is missing.");\n}\n');
    });

    await test.step('Tap on element #TabRecorder', async () => {
      const element_8 = page.locator('#TabRecorder');
      await element_8.click();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: document.querySelector(\'#TabRecorder\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#TabRecorder\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Evaluate Script: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Simulation.SelectedZoomPreset = "50%"', async () => {
      await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Simulation.SelectedZoomPreset = "50%"');
    });

    await test.step('Evaluate Script: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Simulation.ResetPan()', async () => {
      await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Simulation.ResetPan()');
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Evaluate Script: var recorder = ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder;\nif (recorder.IsRecording)\n{\n    recorder.ToggleRecordCommand.Execute(null);\n}\n', async () => {
      await page.evaluate('var recorder = ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder;\nif (recorder.IsRecording)\n{\n    recorder.ToggleRecordCommand.Execute(null);\n}\n');
    });

    await test.step('Evaluate Script: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.ClearCommand.Execute(null)', async () => {
      await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.ClearCommand.Execute(null)');
    });

    await test.step('Evaluate Script: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.ClearRecording()', async () => {
      await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.ClearRecording()');
    });

    await test.step('Delay 300ms', async () => {
      await page.waitForTimeout(300);
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.IsRecordVideoEnabled == true', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.IsRecordVideoEnabled == true');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.IsGenerateReportEnabled == true', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.IsGenerateReportEnabled == true');
      await expect(result).toBeTruthy();
    });

    await test.step('Evaluate Script: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.ToggleRecordCommand.Execute(null)', async () => {
      await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.ToggleRecordCommand.Execute(null)');
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.IsRecording == true', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.IsRecording == true');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #imgScreenshot', async () => {
      await page.mouse.click(126, 509);
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Evaluate Script: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.ToggleRecordCommand.Execute(null)', async () => {
      await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.ToggleRecordCommand.Execute(null)');
    });

    await test.step('Delay 2500ms', async () => {
      await page.waitForTimeout(2500);
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.IsRecording == false', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.IsRecording == false');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.RecordedSteps.Count > 0', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.RecordedSteps.Count > 0');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps.Count == 2', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps.Count == 2');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps[0].Selector != null', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps[0].Selector != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Evaluate Script: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.PlayCommand.Execute(null)', async () => {
      await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.PlayCommand.Execute(null)');
    });

    await test.step('Delay 8000ms', async () => {
      await page.waitForTimeout(8000);
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.IsExecuting == false', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.IsExecuting == false');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps[0].Status == 2', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps[0].Status == 2');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps[1].Status == 2', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps[1].Status == 2');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.LastReportPath != null', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.LastReportPath != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.LastPdfReportPath != null', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.LastPdfReportPath != null');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
