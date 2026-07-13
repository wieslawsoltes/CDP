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

    await test.step('Tap on element #btnRunGC', async () => {
      const element_1 = page.locator('#btnRunGC');
      await element_1.tap();
    });

    await test.step('Delay 2000ms', async () => {
      await page.waitForTimeout(2000);
    });

    await test.step('Tap on element #btnTakeSnapshot', async () => {
      const element_3 = page.locator('#btnTakeSnapshot');
      await element_3.tap();
    });

    await test.step('Delay 3000ms', async () => {
      await page.waitForTimeout(3000);
    });

    await browser.close();
  });
});
