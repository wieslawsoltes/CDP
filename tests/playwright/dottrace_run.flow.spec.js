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

    await test.step('Tap on element #TabProfiler', async () => {
      const element_0 = page.locator('#TabProfiler');
      await element_0.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: document.querySelector(\'#TabProfiler\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#TabProfiler\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #btnStartProfiler', async () => {
      const element_3 = page.locator('#btnStartProfiler');
      await element_3.tap();
    });

    await test.step('Delay 3000ms', async () => {
      await page.waitForTimeout(3000);
    });

    await test.step('Tap on element #btnStopProfiler', async () => {
      const element_5 = page.locator('#btnStopProfiler');
      await element_5.tap();
    });

    await test.step('Delay 2000ms', async () => {
      await page.waitForTimeout(2000);
    });

    await browser.close();
  });
});
