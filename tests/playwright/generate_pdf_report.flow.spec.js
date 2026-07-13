import { test, expect, chromium } from '@playwright/test';

test.describe('CDP Recorded Tests', () => {
  test('recorded test', async () => {
    const browser = await chromium.connectOverCDP('http://localhost:9222');
    const context = browser.contexts()[0];
    const page = context.pages()[0];

    await test.step('Set viewport size', async () => {
      await page.setViewportSize({ width: 800, height: 600 });
    });
    await test.step('Navigate to application', async () => {
      await page.goto('http://localhost:9222/');
    });

    // Warning: Unsupported step type 'runFlow'

    // Warning: Unsupported step type 'runFlow'

    // Warning: Unsupported step type 'evalScript'

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.IsGenerateReportEnabled == true', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.IsGenerateReportEnabled == true');
      await expect(result).toBeTruthy();
    });

    // Warning: Unsupported step type 'evalScript'

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Tap on element #btnTestStudioApplyYaml', async () => {
      const element_7 = page.locator('#btnTestStudioApplyYaml');
      await element_7.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps.Count == 2', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps.Count == 2');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #btnTestStudioPlay', async () => {
      const element_10 = page.locator('#btnTestStudioPlay');
      await element_10.tap();
    });

    await test.step('Delay 4000ms', async () => {
      await page.waitForTimeout(4000);
    });

    await test.step('Assert False: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.IsExecuting == true', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.IsExecuting == true');
      await expect(result).toBeFalsy();
    });

    await test.step('Assert True: !string.IsNullOrEmpty(((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.LastPdfReportPath)', async () => {
      const result = await page.evaluate('!string.IsNullOrEmpty(((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.LastPdfReportPath)');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: System.IO.File.Exists(((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.LastPdfReportPath)', async () => {
      const result = await page.evaluate('System.IO.File.Exists(((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.LastPdfReportPath)');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #btnViewPdfReport', async () => {
      const element_15 = page.locator('#btnViewPdfReport');
      await element_15.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await browser.close();
  });
});
