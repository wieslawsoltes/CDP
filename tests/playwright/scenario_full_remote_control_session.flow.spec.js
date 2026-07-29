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

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Delay 300ms', async () => {
      await page.waitForTimeout(300);
    });

    await test.step('Tap on element #btnRefreshTargets', async () => {
      const element_2 = page.locator('#btnRefreshTargets');
      await element_2.tap();
    });

    await test.step('Delay 300ms', async () => {
      await page.waitForTimeout(300);
    });

    await test.step('Tap on element #btnConnect', async () => {
      const element_4 = page.locator('#btnConnect');
      await element_4.tap();
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Assert True: __raw_window.DataContext.Connection.IsConnected == true', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Connection.IsConnected == true');
      await expect(result).toBeTruthy();
    });

    await test.step('Delay 200ms', async () => {
      await page.waitForTimeout(200);
    });

    await test.step('Tap on element #TabPreview', async () => {
      const element_8 = page.locator('#TabPreview');
      await element_8.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert element #imgScreenshot is visible', async () => {
      await expect(page.locator('#imgScreenshot')).toBeVisible();
    });

    await test.step('Delay 200ms', async () => {
      await page.waitForTimeout(200);
    });

    await test.step('Evaluate Script: document.querySelector(\'#TabRecorder\') != null', async () => {
      await page.evaluate('document.querySelector(\'#TabRecorder\') != null');
    });

    await test.step('Tap on element #btnTestStudioToggleRecord', async () => {
      const element_13 = page.locator('#btnTestStudioToggleRecord');
      await element_13.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: __raw_window.DataContext.Recorder.IsRecording == true', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Recorder.IsRecording == true');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #btnSampleAction', async () => {
      const element_16 = page.locator('#btnSampleAction');
      await element_16.tap();
    });

    await test.step('Delay 300ms', async () => {
      await page.waitForTimeout(300);
    });

    await test.step('Type text in element #txtHost', async () => {
      const element_18 = page.locator('#txtHost');
      await element_18.fill('10.0.0.42');
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Tap on element #TabElements', async () => {
      const element_20 = page.locator('#TabElements');
      await element_20.tap();
    });

    await test.step('Delay 400ms', async () => {
      await page.waitForTimeout(400);
    });

    await test.step('Tap on element #lstVisualTree', async () => {
      const element_22 = page.locator('#lstVisualTree');
      await element_22.tap();
    });

    await test.step('Delay 300ms', async () => {
      await page.waitForTimeout(300);
    });

    await test.step('Assert element #panelSelectedElementProps is visible', async () => {
      await expect(page.locator('#panelSelectedElementProps')).toBeVisible();
    });

    await test.step('Tap on element #btnTestStudioToggleRecord', async () => {
      const element_25 = page.locator('#btnTestStudioToggleRecord');
      await element_25.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Tap on element #btnTestStudioPlay', async () => {
      const element_27 = page.locator('#btnTestStudioPlay');
      await element_27.tap();
    });

    // Warning: Unsupported step type 'extendedWaitUntil'

    await test.step('Assert True: __raw_window.DataContext.Recorder.TestStudio.FailedStepCount == 0', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Recorder.TestStudio.FailedStepCount == 0');
      await expect(result).toBeTruthy();
    });

    await test.step('Delay 200ms', async () => {
      await page.waitForTimeout(200);
    });

    await test.step('Tap on element #btnDisconnect', async () => {
      const element_31 = page.locator('#btnDisconnect');
      await element_31.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: __raw_window.DataContext.Connection.IsConnected == false', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Connection.IsConnected == false');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
