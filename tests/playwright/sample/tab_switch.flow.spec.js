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

    await test.step('Assert element #btnClickMe is visible', async () => {
      await expect(page.locator('#btnClickMe')).toBeVisible();
    });

    await test.step('Tap on element #tabScroll', async () => {
      const element_2 = page.locator('#tabScroll');
      await element_2.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert element #scrollContainer is visible', async () => {
      await expect(page.locator('#scrollContainer')).toBeVisible();
    });

    await test.step('Tap on element #tabAbout', async () => {
      const element_5 = page.locator('#tabAbout');
      await element_5.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert element #btnGoBack is visible', async () => {
      await expect(page.locator('#btnGoBack')).toBeVisible();
    });

    await test.step('Tap on element #btnGoBack', async () => {
      const element_8 = page.locator('#btnGoBack');
      await element_8.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert element #btnClickMe is visible', async () => {
      await expect(page.locator('#btnClickMe')).toBeVisible();
    });

    await browser.close();
  });
});
