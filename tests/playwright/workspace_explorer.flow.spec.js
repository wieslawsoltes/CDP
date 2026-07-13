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

    await test.step('Tap on element #TabSources', async () => {
      const element_0 = page.locator('#TabSources');
      await element_0.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: document.querySelector(\'#TabSources\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#TabSources\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert element #treeWorkspaceFiles is visible', async () => {
      await expect(page.locator('#treeWorkspaceFiles')).toBeVisible();
    });

    await test.step('Assert element #txtSourcesSearch is visible', async () => {
      await expect(page.locator('#txtSourcesSearch')).toBeVisible();
    });

    await test.step('Type text in element #txtSourcesSearch', async () => {
      const element_5 = page.locator('#txtSourcesSearch');
      await element_5.fill('flow');
    });

    await test.step('Tap on element #btnSearchWorkspace', async () => {
      const element_6 = page.locator('#btnSearchWorkspace');
      await element_6.tap();
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
