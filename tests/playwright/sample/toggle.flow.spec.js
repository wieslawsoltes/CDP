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

    await test.step('Assert element #chkToggle is visible', async () => {
      await expect(page.locator('#chkToggle')).toBeVisible();
    });

    await test.step('Assert True: document.querySelector(\'#chkToggle\').isChecked == false', async () => {
      const result = await page.evaluate('document.querySelector(\'#chkToggle\').isChecked == false');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #chkToggle', async () => {
      const element_2 = page.locator('#chkToggle');
      await element_2.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: document.querySelector(\'#chkToggle\').isChecked == true', async () => {
      const result = await page.evaluate('document.querySelector(\'#chkToggle\').isChecked == true');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #chkToggle', async () => {
      const element_5 = page.locator('#chkToggle');
      await element_5.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: document.querySelector(\'#chkToggle\').isChecked == false', async () => {
      const result = await page.evaluate('document.querySelector(\'#chkToggle\').isChecked == false');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
