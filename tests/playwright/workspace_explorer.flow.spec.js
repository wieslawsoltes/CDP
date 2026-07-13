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

    await test.step('Assert element #treeWorkspaceFiles is visible', async () => {
      await expect(page.locator('#treeWorkspaceFiles')).toBeVisible();
    });

    await test.step('Assert element #txtSourcesSearch is visible', async () => {
      await expect(page.locator('#txtSourcesSearch')).toBeVisible();
    });

    await test.step('Type text in element #txtSourcesSearch', async () => {
      const element_3 = page.locator('#txtSourcesSearch');
      await element_3.fill('flow');
    });

    await test.step('Tap on element #btnSearchWorkspace', async () => {
      const element_4 = page.locator('#btnSearchWorkspace');
      await element_4.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert element #dgSearchResults is visible', async () => {
      await expect(page.locator('#dgSearchResults')).toBeVisible();
    });

    await browser.close();
  });
});
