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

    await test.step('Tap on element #txtInput', async () => {
      const element_1 = page.locator('#txtInput');
      await element_1.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Clear text in element #txtInput', async () => {
      const element_3 = page.locator('#txtInput');
      await element_3.clear();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Type text in element #txtInput', async () => {
      const element_5 = page.locator('#txtInput');
      await element_5.fill('Hello CDP E2E!');
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert element #txtInput[Text=\'Hello CDP E2E!\'] is visible', async () => {
      await expect(page.locator('#txtInput[Text=\'Hello CDP E2E!\']')).toBeVisible();
    });

    await browser.close();
  });
});
