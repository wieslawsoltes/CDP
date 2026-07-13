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

    await test.step('Type text in element #txtConsoleInput', async () => {
      const element_3 = page.locator('#txtConsoleInput');
      await element_3.fill('System.Console.WriteLine("UniqueFilterTargetLog")');
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Tap on element #btnSendConsole', async () => {
      const element_5 = page.locator('#btnSendConsole');
      await element_5.tap();
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Assert True: Window.DataContext.Console.Logs.Count > 0', async () => {
      const result = await page.evaluate('Window.DataContext.Console.Logs.Count > 0');
      await expect(result).toBeTruthy();
    });

    await test.step('Type text in element #btnConsoleFilter', async () => {
      const element_8 = page.locator('#btnConsoleFilter');
      await element_8.fill('UniqueFilterTargetLog');
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
      const element_12 = page.locator('#btnConsoleFilter');
      await element_12.clear();
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
