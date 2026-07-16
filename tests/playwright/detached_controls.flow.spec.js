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

    await test.step('Assert True: __raw_window.DataContext.Connection.IsConnected', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Connection.IsConnected');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #TabMemory', async () => {
      const element_6 = page.locator('#TabMemory');
      await element_6.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: document.querySelector(\'#TabMemory\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#TabMemory\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Tap on element #btnTakeSnapshot', async () => {
      const element_10 = page.locator('#btnTakeSnapshot');
      await element_10.tap();
    });

    await test.step('Delay 5000ms', async () => {
      await page.waitForTimeout(5000);
    });

    await test.step('Tap on element #TabDetachedControls', async () => {
      const element_12 = page.locator('#TabDetachedControls');
      await element_12.tap();
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Assert True: __raw_window.DataContext.Memory.DetachedControls != null', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Memory.DetachedControls != null');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
