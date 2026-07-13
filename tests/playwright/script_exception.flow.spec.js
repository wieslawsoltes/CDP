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

    await test.step('Type text in element #txtConsoleInput', async () => {
      const element_1 = page.locator('#txtConsoleInput');
      await element_1.fill('throw new Error("Test exception")');
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Tap on element #btnConsoleRun', async () => {
      const element_3 = page.locator('#btnConsoleRun');
      await element_3.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: Window.DataContext.Console.ConsoleHistory.Count > 0', async () => {
      const result = await page.evaluate('Window.DataContext.Console.ConsoleHistory.Count > 0');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: Window.DataContext.Console.ConsoleHistory[Window.DataContext.Console.ConsoleHistory.Count - 1].IsError', async () => {
      const result = await page.evaluate('Window.DataContext.Console.ConsoleHistory[Window.DataContext.Console.ConsoleHistory.Count - 1].IsError');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
