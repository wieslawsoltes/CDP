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

    await test.step('Tap on element #TabElements', async () => {
      const element_0 = page.locator('#TabElements');
      await element_0.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: document.querySelector(\'#TabElements\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#TabElements\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #TabDomTree', async () => {
      const element_3 = page.locator('#TabDomTree');
      await element_3.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Tap on element #chkVisualTree', async () => {
      const element_5 = page.locator('#chkVisualTree');
      await element_5.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Type text in element #txtSearch', async () => {
      const element_7 = page.locator('#txtSearch');
      await element_7.fill('MainWindow');
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Tap on element #btnSearch', async () => {
      const element_9 = page.locator('#btnSearch');
      await element_9.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: Window.DataContext.Elements.SelectedNodeNode != null', async () => {
      const result = await page.evaluate('Window.DataContext.Elements.SelectedNodeNode != null');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
