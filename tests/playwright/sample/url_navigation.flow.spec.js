import { test, expect, chromium } from '@playwright/test';

test.describe('CDP Recorded Tests', () => {
  test('recorded test', async () => {
    const browser = await chromium.connectOverCDP('http://localhost:9222');
    const context = browser.contexts()[0];
    const page = context.pages()[0];

    await test.step('Set viewport size', async () => {
      await page.setViewportSize({ width: 800, height: 600 });
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert element #btnClickMe is visible', async () => {
      await expect(page.locator('#btnClickMe')).toBeVisible();
    });

    await test.step('Navigate to http://localhost:9222/about', async () => {
      await page.goto('http://localhost:9222/about');
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert element #btnGoBack is visible', async () => {
      await expect(page.locator('#btnGoBack')).toBeVisible();
    });

    await test.step('Navigate to http://localhost:9222/scroll', async () => {
      await page.goto('http://localhost:9222/scroll');
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert element #scrollContainer is visible', async () => {
      await expect(page.locator('#scrollContainer')).toBeVisible();
    });

    await test.step('Navigate to http://localhost:9222/', async () => {
      await page.goto('http://localhost:9222/');
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
