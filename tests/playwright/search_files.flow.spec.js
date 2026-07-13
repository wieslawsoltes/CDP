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

    await test.step('Assert element #txtSourcesSearch is visible', async () => {
      await expect(page.locator('#txtSourcesSearch')).toBeVisible();
    });

    await test.step('Type text in element #txtSourcesSearch', async () => {
      const element_2 = page.locator('#txtSourcesSearch');
      await element_2.fill('flow');
    });

    await test.step('Tap on element #btnSearchWorkspace', async () => {
      const element_3 = page.locator('#btnSearchWorkspace');
      await element_3.tap();
    });

    await test.step('Delay 2000ms', async () => {
      await page.waitForTimeout(2000);
    });

    await test.step('Assert element #dgSearchResults is visible', async () => {
      await expect(page.locator('#dgSearchResults')).toBeVisible();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Sources.SearchQuery == "flow"', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Sources.SearchQuery == "flow"');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Sources.SearchResults != null', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Sources.SearchResults != null');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
