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

    await test.step('Assert element #btnNetworkClear is visible', async () => {
      await expect(page.locator('#btnNetworkClear')).toBeVisible();
    });

    await test.step('Evaluate Script: var vm = (CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext; var model = new CdpInspectorApp.Models.NetworkRequestModel { RequestId = "req-to-clear", Url = "http://example.com/clear", Method = "POST" }; vm.Network.NetworkRequests.Add(model);', async () => {
      await page.evaluate('var vm = (CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext; var model = new CdpInspectorApp.Models.NetworkRequestModel { RequestId = "req-to-clear", Url = "http://example.com/clear", Method = "POST" }; vm.Network.NetworkRequests.Add(model);');
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.NetworkRequests.Count > 0', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.NetworkRequests.Count > 0');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #btnNetworkClear', async () => {
      const element_7 = page.locator('#btnNetworkClear');
      await element_7.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.NetworkRequests.Count == 0', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.NetworkRequests.Count == 0');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
