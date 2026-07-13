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

    await test.step('Tap on element #TabConsole', async () => {
      const element_0 = page.locator('#TabConsole');
      await element_0.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: document.querySelector(\'#TabConsole\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#TabConsole\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Tap on element #btnConsoleClear', async () => {
      const element_4 = page.locator('#btnConsoleClear');
      await element_4.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: Window.DataContext.Console.Logs.Count == 0', async () => {
      const result = await page.evaluate('Window.DataContext.Console.Logs.Count == 0');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
