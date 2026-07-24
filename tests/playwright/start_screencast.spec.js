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

    await test.step('Delay 200ms', async () => {
      await page.waitForTimeout(200);
    });

    await test.step('Evaluate Script: document.querySelector(\'#TabRecorder\') != null', async () => {
      await page.evaluate('document.querySelector(\'#TabRecorder\') != null');
    });

    await test.step('Tap on element #btnTestStudioToggleRecord', async () => {
      const element_2 = page.locator('#btnTestStudioToggleRecord');
      await element_2.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: __raw_window.DataContext.Recorder.IsRecording == true', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Recorder.IsRecording == true');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
