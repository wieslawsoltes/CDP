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

    // Warning: Unsupported step type 'runFlow'

    await test.step('Tap on element TabItem:has-text("Computed rules")', async () => {
      const element_1 = page.locator('TabItem:has-text("Computed rules")');
      await element_1.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Type text in element #txtComputedSearch', async () => {
      const element_3 = page.locator('#txtComputedSearch');
      await element_3.fill('Background');
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: Window.DataContext.Elements.ComputedSearchText == "Background"', async () => {
      const result = await page.evaluate('Window.DataContext.Elements.ComputedSearchText == "Background"');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: Window.DataContext.Elements.FilteredComputedStyles.Count >= 0', async () => {
      const result = await page.evaluate('Window.DataContext.Elements.FilteredComputedStyles.Count >= 0');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
