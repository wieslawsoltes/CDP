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

    await test.step('Tap on element #TabNetwork', async () => {
      const element_0 = page.locator('#TabNetwork');
      await element_0.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: document.querySelector(\'#TabNetwork\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#TabNetwork\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert element #lstNetworkRequests is visible', async () => {
      await expect(page.locator('#lstNetworkRequests')).toBeVisible();
    });

    await test.step('Assert element #btnNetworkClear is visible', async () => {
      await expect(page.locator('#btnNetworkClear')).toBeVisible();
    });

    await test.step('Assert element #cmbThrottling is visible', async () => {
      await expect(page.locator('#cmbThrottling')).toBeVisible();
    });

    await test.step('Tap on element #TabMockingRules', async () => {
      const element_6 = page.locator('#TabMockingRules');
      await element_6.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert element #btnAddMockRule is visible', async () => {
      await expect(page.locator('#btnAddMockRule')).toBeVisible();
    });

    await test.step('Tap on element #btnAddMockRule', async () => {
      const element_9 = page.locator('#btnAddMockRule');
      await element_9.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert element #lstMockRules is visible', async () => {
      await expect(page.locator('#lstMockRules')).toBeVisible();
    });

    await browser.close();
  });
});
