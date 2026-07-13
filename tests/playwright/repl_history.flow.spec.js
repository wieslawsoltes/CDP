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
      await element_3.fill('10 + 20');
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Tap on element #btnSendConsole', async () => {
      const element_5 = page.locator('#btnSendConsole');
      await element_5.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Type text in element #txtConsoleInput', async () => {
      const element_7 = page.locator('#txtConsoleInput');
      await element_7.fill('30 + 40');
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Tap on element #btnSendConsole', async () => {
      const element_9 = page.locator('#btnSendConsole');
      await element_9.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Type text in element #txtConsoleInput', async () => {
      const element_11 = page.locator('#txtConsoleInput');
      await element_11.fill('50 + 60');
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Tap on element #btnSendConsole', async () => {
      const element_13 = page.locator('#btnSendConsole');
      await element_13.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: Window.DataContext.Console.ConsoleInputText == ""', async () => {
      const result = await page.evaluate('Window.DataContext.Console.ConsoleInputText == ""');
      await expect(result).toBeTruthy();
    });

    await test.step('Press key Up', async () => {
      await page.keyboard.press('Up');
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: Window.DataContext.Console.ConsoleInputText == "50 + 60"', async () => {
      const result = await page.evaluate('Window.DataContext.Console.ConsoleInputText == "50 + 60"');
      await expect(result).toBeTruthy();
    });

    await test.step('Press key Up', async () => {
      await page.keyboard.press('Up');
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: Window.DataContext.Console.ConsoleInputText == "30 + 40"', async () => {
      const result = await page.evaluate('Window.DataContext.Console.ConsoleInputText == "30 + 40"');
      await expect(result).toBeTruthy();
    });

    await test.step('Press key Up', async () => {
      await page.keyboard.press('Up');
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: Window.DataContext.Console.ConsoleInputText == "10 + 20"', async () => {
      const result = await page.evaluate('Window.DataContext.Console.ConsoleInputText == "10 + 20"');
      await expect(result).toBeTruthy();
    });

    await test.step('Press key Down', async () => {
      await page.keyboard.press('Down');
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: Window.DataContext.Console.ConsoleInputText == "30 + 40"', async () => {
      const result = await page.evaluate('Window.DataContext.Console.ConsoleInputText == "30 + 40"');
      await expect(result).toBeTruthy();
    });

    await test.step('Press key Down', async () => {
      await page.keyboard.press('Down');
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: Window.DataContext.Console.ConsoleInputText == "50 + 60"', async () => {
      const result = await page.evaluate('Window.DataContext.Console.ConsoleInputText == "50 + 60"');
      await expect(result).toBeTruthy();
    });

    await test.step('Press key Down', async () => {
      await page.keyboard.press('Down');
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: Window.DataContext.Console.ConsoleInputText == ""', async () => {
      const result = await page.evaluate('Window.DataContext.Console.ConsoleInputText == ""');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
