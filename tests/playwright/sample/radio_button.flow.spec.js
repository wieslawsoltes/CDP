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

    await test.step('Tap on element #rbOption2', async () => {
      const element_1 = page.locator('#rbOption2');
      await element_1.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: document.querySelector(\'#rbOption2\').isChecked', async () => {
      const result = await page.evaluate('document.querySelector(\'#rbOption2\').isChecked');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: !document.querySelector(\'#rbOption1\').isChecked', async () => {
      const result = await page.evaluate('!document.querySelector(\'#rbOption1\').isChecked');
      await expect(result).toBeTruthy();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Tap on element #rbOption1', async () => {
      const element_6 = page.locator('#rbOption1');
      await element_6.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: document.querySelector(\'#rbOption1\').isChecked', async () => {
      const result = await page.evaluate('document.querySelector(\'#rbOption1\').isChecked');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: !document.querySelector(\'#rbOption2\').isChecked', async () => {
      const result = await page.evaluate('!document.querySelector(\'#rbOption2\').isChecked');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
