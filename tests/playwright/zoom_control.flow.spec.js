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

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Tap on element #TabScratch', async () => {
      const element_1 = page.locator('#TabScratch');
      await element_1.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: __raw_window.DataContext.Scratch.Zoom == 1.0', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Scratch.Zoom == 1.0');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #btnZoomIn', async () => {
      const element_4 = page.locator('#btnZoomIn');
      await element_4.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: __raw_window.DataContext.Scratch.Zoom > 1.1 && __raw_window.DataContext.Scratch.Zoom < 1.3', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Scratch.Zoom > 1.1 && __raw_window.DataContext.Scratch.Zoom < 1.3');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #btnZoomOut', async () => {
      const element_7 = page.locator('#btnZoomOut');
      await element_7.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: __raw_window.DataContext.Scratch.Zoom > 0.9 && __raw_window.DataContext.Scratch.Zoom < 1.0', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Scratch.Zoom > 0.9 && __raw_window.DataContext.Scratch.Zoom < 1.0');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
