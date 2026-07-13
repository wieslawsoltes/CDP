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
      await element_1.fill('System.Console.WriteLine("UniqueFilterTargetLog")');
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Tap on element #btnSendConsole', async () => {
      const element_3 = page.locator('#btnSendConsole');
      await element_3.tap();
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Assert True: Window.DataContext.Console.Logs.Count > 0', async () => {
      const result = await page.evaluate('Window.DataContext.Console.Logs.Count > 0');
      await expect(result).toBeTruthy();
    });

    await test.step('Type text in element #btnConsoleFilter', async () => {
      const element_6 = page.locator('#btnConsoleFilter');
      await element_6.fill('UniqueFilterTargetLog');
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: Window.DataContext.Console.Logs.Count == 1', async () => {
      const result = await page.evaluate('Window.DataContext.Console.Logs.Count == 1');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: Window.DataContext.Console.Logs[0].Text.Contains("UniqueFilterTargetLog")', async () => {
      const result = await page.evaluate('Window.DataContext.Console.Logs[0].Text.Contains("UniqueFilterTargetLog")');
      await expect(result).toBeTruthy();
    });

    await test.step('Clear text in element #btnConsoleFilter', async () => {
      const element_10 = page.locator('#btnConsoleFilter');
      await element_10.clear();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: Window.DataContext.Console.Logs.Count > 1', async () => {
      const result = await page.evaluate('Window.DataContext.Console.Logs.Count > 1');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
