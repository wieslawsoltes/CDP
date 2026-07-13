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

    await test.step('Assert element #lstApplicationResources is visible', async () => {
      await expect(page.locator('#lstApplicationResources')).toBeVisible();
    });

    await test.step('Tap on element #btnRefreshResources', async () => {
      const element_2 = page.locator('#btnRefreshResources');
      await element_2.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Type text in element #txtResourceKey', async () => {
      const element_4 = page.locator('#txtResourceKey');
      await element_4.fill('TestColor');
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Type text in element #txtResourceValue', async () => {
      const element_6 = page.locator('#txtResourceValue');
      await element_6.fill('#FF0000');
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Tap on element #btnAddResource', async () => {
      const element_8 = page.locator('#btnAddResource');
      await element_8.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: true', async () => {
      const result = await page.evaluate('true');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
