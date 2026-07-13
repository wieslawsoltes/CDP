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

    await test.step('Assert element #txtNetworkFilter is visible', async () => {
      await expect(page.locator('#txtNetworkFilter')).toBeVisible();
    });

    await test.step('Assert element #lstNetworkRequests is visible', async () => {
      await expect(page.locator('#lstNetworkRequests')).toBeVisible();
    });

    // Warning: Unsupported step type 'evalScript'

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.FilteredRequestsCount == 2', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.FilteredRequestsCount == 2');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #txtNetworkFilter', async () => {
      const element_6 = page.locator('#txtNetworkFilter');
      await element_6.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Type text into focused element', async () => {
      await page.keyboard.type('users');
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.FilteredRequestsCount == 1', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.FilteredRequestsCount == 1');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #txtNetworkFilter', async () => {
      const element_11 = page.locator('#txtNetworkFilter');
      await element_11.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Clear text in element #txtNetworkFilter', async () => {
      const element_13 = page.locator('#txtNetworkFilter');
      await element_13.clear();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.FilteredRequestsCount == 2', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.FilteredRequestsCount == 2');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
