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

    await test.step('Type text in element #txtUsername', async () => {
      const element_1 = page.locator('#txtUsername');
      await element_1.fill('rdp_user');
    });

    await test.step('Type text in element #txtPassword', async () => {
      const element_2 = page.locator('#txtPassword');
      await element_2.fill('Pass1234!');
    });

    await test.step('Tap on element #btnConnect', async () => {
      const element_3 = page.locator('#btnConnect');
      await element_3.tap();
    });

    await test.step('Delay 2000ms', async () => {
      await page.waitForTimeout(2000);
    });

    await test.step('Assert True: __raw_window.DataContext.Connection.IsNlaAuthenticated == true', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Connection.IsNlaAuthenticated == true');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
