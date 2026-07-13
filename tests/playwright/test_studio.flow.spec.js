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

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Tap on element #btnRefreshTargets', async () => {
      const element_1 = page.locator('#btnRefreshTargets');
      await element_1.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Tap on element #btnConnect', async () => {
      const element_3 = page.locator('#btnConnect');
      await element_3.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: __raw_window.DataContext.Connection.IsConnected', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Connection.IsConnected');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #TabRecorder', async () => {
      const element_6 = page.locator('#TabRecorder');
      await element_6.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: document.querySelector(\'#TabRecorder\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#TabRecorder\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps.Count == 0', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps.Count == 0');
      await expect(result).toBeTruthy();
    });

    await test.step('Evaluate Script: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.YamlCode = "appId: \\"CdpSampleApp\\"\\ndescription: \\"Applied Flow\\"\\n---\\n- delay: 1000\\n- delay: 2000\\n"', async () => {
      await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.YamlCode = "appId: \\"CdpSampleApp\\"\\ndescription: \\"Applied Flow\\"\\n---\\n- delay: 1000\\n- delay: 2000\\n"');
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Tap on element #btnTestStudioApplyYaml', async () => {
      const element_12 = page.locator('#btnTestStudioApplyYaml');
      await element_12.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps.Count == 2', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps.Count == 2');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps[0].Action == "delay" && ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps[0].Value == "1000"', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps[0].Action == "delay" && ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps[0].Value == "1000"');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps[1].Action == "delay" && ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps[1].Value == "2000"', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps[1].Action == "delay" && ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps[1].Value == "2000"');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #btnTestStudioPlay', async () => {
      const element_17 = page.locator('#btnTestStudioPlay');
      await element_17.tap();
    });

    await test.step('Delay 4000ms', async () => {
      await page.waitForTimeout(4000);
    });

    await test.step('Assert False: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.IsExecuting == true', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.IsExecuting == true');
      await expect(result).toBeFalsy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps[0].Status.ToString() == "Passed"', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps[0].Status.ToString() == "Passed"');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps[1].Status.ToString() == "Passed"', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps[1].Status.ToString() == "Passed"');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
