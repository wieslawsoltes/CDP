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

    await test.step('Clear text in element #txtPort', async () => {
      const element_0 = page.locator('#txtPort');
      await element_0.clear();
    });

    await test.step('Type text in element #txtPort', async () => {
      const element_1 = page.locator('#txtPort');
      await element_1.fill('9999');
    });

    await test.step('Tap on element #btnConnect', async () => {
      const element_2 = page.locator('#btnConnect');
      await element_2.tap();
    });

    await test.step('Delay 2000ms', async () => {
      await page.waitForTimeout(2000);
    });

    await test.step('Assert True: __raw_window.DataContext.Connection.IsConnected == false', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Connection.IsConnected == false');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert element #lblPortError is visible', async () => {
      await expect(page.locator('#lblPortError')).toBeVisible();
    });

    await browser.close();
  });
});
