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

    await test.step('Tap on element #chkTestStudioRecordVideo', async () => {
      const element_2 = page.locator('#chkTestStudioRecordVideo');
      await element_2.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.IsRecordVideoEnabled == false', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.IsRecordVideoEnabled == false');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #chkTestStudioGenerateReports', async () => {
      const element_5 = page.locator('#chkTestStudioGenerateReports');
      await element_5.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.IsGenerateReportEnabled == false', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.IsGenerateReportEnabled == false');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #chkTestStudioRecordVideo', async () => {
      const element_8 = page.locator('#chkTestStudioRecordVideo');
      await element_8.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.IsRecordVideoEnabled == true', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.IsRecordVideoEnabled == true');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #chkTestStudioGenerateReports', async () => {
      const element_11 = page.locator('#chkTestStudioGenerateReports');
      await element_11.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.IsGenerateReportEnabled == true', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.IsGenerateReportEnabled == true');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #btnTestStudioToggleRecord', async () => {
      const element_14 = page.locator('#btnTestStudioToggleRecord');
      await element_14.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.IsRecording == true', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.IsRecording == true');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #btnTestStudioToggleRecord', async () => {
      const element_17 = page.locator('#btnTestStudioToggleRecord');
      await element_17.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.IsRecording == false', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.IsRecording == false');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
