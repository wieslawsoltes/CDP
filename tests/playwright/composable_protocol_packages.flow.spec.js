import { test, expect, chromium } from '@playwright/test';

test.describe('CDP Recorded Tests', () => {
  test('recorded test', async () => {
    const browser = await chromium.connectOverCDP('http://127.0.0.1:9223');
    const context = browser.contexts()[0];
    const page = context.pages()[0];

    await test.step('Set viewport size', async () => {
      await page.setViewportSize({ width: 800, height: 600 });
    });
    await test.step('Navigate to application', async () => {
      await page.goto('http://127.0.0.1:9223/');
    });

    await test.step('Evaluate Script: ((CdpInspectorApp.ViewModels.MainWindowViewModel)__raw_window.DataContext).Connection.RefreshTargetsAsync()', async () => {
      await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)__raw_window.DataContext).Connection.RefreshTargetsAsync()');
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Evaluate Script: ((CdpInspectorApp.ViewModels.MainWindowViewModel)__raw_window.DataContext).Connection.ConnectAsync()', async () => {
      await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)__raw_window.DataContext).Connection.ConnectAsync()');
    });

    await test.step('Delay 3000ms', async () => {
      await page.waitForTimeout(3000);
    });

    await test.step('Assert True: __raw_window.DataContext.Connection.IsConnected', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Connection.IsConnected');
      await expect(result).toBeTruthy();
    });

    await test.step('Evaluate Script: ((CdpInspectorApp.ViewModels.MainWindowViewModel)__raw_window.DataContext).IsPreviewPanelVisible = true', async () => {
      await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)__raw_window.DataContext).IsPreviewPanelVisible = true');
    });

    await test.step('Evaluate Script: ((CdpInspectorApp.ViewModels.MainWindowViewModel)__raw_window.DataContext).NavigateToView("Recorder")', async () => {
      await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)__raw_window.DataContext).NavigateToView("Recorder")');
    });

    await test.step('Evaluate Script: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Simulation.SelectedZoomPreset = "50%"', async () => {
      await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Simulation.SelectedZoomPreset = "50%"');
    });

    await test.step('Evaluate Script: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Simulation.ResetPan()', async () => {
      await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Simulation.ResetPan()');
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

    await test.step('Assert False: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.IsRecording == true', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.IsRecording == true');
      await expect(result).toBeFalsy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.RecordedSteps.Count > 0', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.RecordedSteps.Count > 0');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps.Count > 0', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps.Count > 0');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps[0].Selector == "#btnClickMe"', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps[0].Selector == "#btnClickMe"');
      await expect(result).toBeTruthy();
    });

    await test.step('Evaluate Script: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.PlayCommand.Execute(null)', async () => {
      await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.PlayCommand.Execute(null)');
    });

    await test.step('Delay 8000ms', async () => {
      await page.waitForTimeout(8000);
    });

    await test.step('Assert False: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.IsExecuting == true', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.IsExecuting == true');
      await expect(result).toBeFalsy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps[0].Status == 2', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps[0].Status == 2');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Simulation.ScreenshotImage != null', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Simulation.ScreenshotImage != null');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
