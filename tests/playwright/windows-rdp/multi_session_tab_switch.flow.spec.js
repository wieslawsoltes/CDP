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

    await test.step('Delay 300ms', async () => {
      await page.waitForTimeout(300);
    });

    await test.step('Tap on element #tabWorkspace', async () => {
      const element_1 = page.locator('#tabWorkspace');
      await element_1.click();
    });

    await test.step('Delay 300ms', async () => {
      await page.waitForTimeout(300);
    });

    await test.step('Assert True: document.querySelector(\'#tabWorkspace\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#tabWorkspace\') != null');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
