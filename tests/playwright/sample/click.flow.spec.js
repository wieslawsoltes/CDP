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

    await test.step('Assert True: document.querySelector(\'#txtStatus\').textContent == "Not Clicked"', async () => {
      const result = await page.evaluate('document.querySelector(\'#txtStatus\').textContent == "Not Clicked"');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #btnClickMe', async () => {
      const element_2 = page.locator('#btnClickMe');
      await element_2.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: document.querySelector(\'#txtStatus\').textContent == "Clicked 1 times!"', async () => {
      const result = await page.evaluate('document.querySelector(\'#txtStatus\').textContent == "Clicked 1 times!"');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
