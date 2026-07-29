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

    await test.step('Tap on element #txtHost', async () => {
      const element_6 = page.locator('#txtHost');
      await element_6.tap();
    });

    await test.step('Type text in element #txtHost', async () => {
      const element_7 = page.locator('#txtHost');
      await element_7.fill('127.0.0.1');
    });

    await test.step('Assert True: __raw_window.DataContext.Connection.Host == \'127.0.0.1\'', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Connection.Host == \'127.0.0.1\'');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
