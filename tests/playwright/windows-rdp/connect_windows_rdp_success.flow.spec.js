import { test, expect, chromium } from '@playwright/test';

test.describe('CDP Recorded Tests', () => {
  test('recorded test', async () => {
    const browser = await chromium.connectOverCDP('http://127.0.0.1:9225');
    const context = browser.contexts()[0];
    const page = context.pages()[0];

    await test.step('Set viewport size', async () => {
      await page.setViewportSize({ width: 800, height: 600 });
    });
    await test.step('Navigate to application', async () => {
      await page.goto('http://127.0.0.1:9225/');
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: document.querySelector(\'#btnQuickConnect\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#btnQuickConnect\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #btnQuickConnect', async () => {
      const element_2 = page.locator('#btnQuickConnect');
      await element_2.click();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: document.querySelector(\'#tabWorkspace\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#tabWorkspace\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: document.querySelector(\'#navSidebar\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#navSidebar\') != null');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
