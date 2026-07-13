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

    await test.step('Assert element #txtNetworkFilter is visible', async () => {
      await expect(page.locator('#txtNetworkFilter')).toBeVisible();
    });

    await test.step('Assert element #lstNetworkRequests is visible', async () => {
      await expect(page.locator('#lstNetworkRequests')).toBeVisible();
    });

    await test.step('Evaluate Script: var vm = (CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext; vm.Network.NetworkRequests.Clear(); vm.Network.NetworkRequests.Add(new CdpInspectorApp.Models.NetworkRequestModel { RequestId = "req-1", Url = "http://example.com/api/users", Method = "GET", Type = "XHR" }); vm.Network.NetworkRequests.Add(new CdpInspectorApp.Models.NetworkRequestModel { RequestId = "req-2", Url = "http://example.com/static/style.css", Method = "GET", Type = "Stylesheet" });', async () => {
      await page.evaluate('var vm = (CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext; vm.Network.NetworkRequests.Clear(); vm.Network.NetworkRequests.Add(new CdpInspectorApp.Models.NetworkRequestModel { RequestId = "req-1", Url = "http://example.com/api/users", Method = "GET", Type = "XHR" }); vm.Network.NetworkRequests.Add(new CdpInspectorApp.Models.NetworkRequestModel { RequestId = "req-2", Url = "http://example.com/static/style.css", Method = "GET", Type = "Stylesheet" });');
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.FilteredRequestsCount == 2', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.FilteredRequestsCount == 2');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #txtNetworkFilter', async () => {
      const element_8 = page.locator('#txtNetworkFilter');
      await element_8.tap();
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
      const element_13 = page.locator('#txtNetworkFilter');
      await element_13.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Clear text in element #txtNetworkFilter', async () => {
      const element_15 = page.locator('#txtNetworkFilter');
      await element_15.clear();
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
