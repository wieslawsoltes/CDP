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

    await test.step('Delay 300ms', async () => {
      await page.waitForTimeout(300);
    });

    await test.step('Tap on element #btnRefreshTargets', async () => {
      const element_1 = page.locator('#btnRefreshTargets');
      await element_1.tap();
    });

    await test.step('Delay 300ms', async () => {
      await page.waitForTimeout(300);
    });

    await test.step('Tap on element #btnConnect', async () => {
      const element_3 = page.locator('#btnConnect');
      await element_3.tap();
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Assert True: __raw_window.DataContext.Connection.IsConnected == true', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Connection.IsConnected == true');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #TabElements', async () => {
      const element_6 = page.locator('#TabElements');
      await element_6.tap();
    });

    await test.step('Delay 400ms', async () => {
      await page.waitForTimeout(400);
    });

    await test.step('Tap on element #lstVisualTree', async () => {
      const element_8 = page.locator('#lstVisualTree');
      await element_8.tap();
    });

    await test.step('Delay 300ms', async () => {
      await page.waitForTimeout(300);
    });

    await test.step('Assert element #panelSelectedElementProps is visible', async () => {
      await expect(page.locator('#panelSelectedElementProps')).toBeVisible();
    });

    await test.step('Assert element #txtSelectedControlName is visible', async () => {
      await expect(page.locator('#txtSelectedControlName')).toBeVisible();
    });

    await browser.close();
  });
});
