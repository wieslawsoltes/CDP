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

    await test.step('Tap on element #tabAssertsAndKeys', async () => {
      const element_1 = page.locator('#tabAssertsAndKeys');
      await element_1.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert element #txtVisibilityTarget is visible', async () => {
      await expect(page.locator('#txtVisibilityTarget')).toBeVisible();
    });

    await test.step('Tap on element #btnToggleVisibility', async () => {
      const element_4 = page.locator('#btnToggleVisibility');
      await element_4.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert element #txtVisibilityTarget is hidden', async () => {
      await expect(page.locator('#txtVisibilityTarget')).toBeHidden();
    });

    await test.step('Tap on element #txtKeyInput', async () => {
      const element_7 = page.locator('#txtKeyInput');
      await element_7.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Press key Enter', async () => {
      await page.keyboard.press('Enter');
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: Window.DataContext.LastPressedKey == "Enter"', async () => {
      const result = await page.evaluate('Window.DataContext.LastPressedKey == "Enter"');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
