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

    await test.step('Evaluate Script: var vm = (CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext; var model = new CdpInspectorApp.Models.NetworkRequestModel { RequestId = "req-test-payload", Url = "http://example.com/api?user=john_doe&action=login", Method = "POST", Type = "XHR", Status = "200 OK", Time = "12 ms" }; model.ParsePostParameters("client=desktop&version=1.0"); vm.Network.NetworkRequests.Add(model); vm.Network.SelectedRequest = model;', async () => {
      await page.evaluate('var vm = (CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext; var model = new CdpInspectorApp.Models.NetworkRequestModel { RequestId = "req-test-payload", Url = "http://example.com/api?user=john_doe&action=login", Method = "POST", Type = "XHR", Status = "200 OK", Time = "12 ms" }; model.ParsePostParameters("client=desktop&version=1.0"); vm.Network.NetworkRequests.Add(model); vm.Network.SelectedRequest = model;');
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Tap on element TextBlock:has-text(\'Payload\')', async () => {
      const element_6 = page.locator('TextBlock:has-text(\'Payload\')');
      await element_6.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.SelectedRequest.HasPayload', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.SelectedRequest.HasPayload');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.SelectedRequest.QueryParameters.Count == 2', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.SelectedRequest.QueryParameters.Count == 2');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.SelectedRequest.QueryParameters[0].Key == "user"', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.SelectedRequest.QueryParameters[0].Key == "user"');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.SelectedRequest.QueryParameters[0].Value == "john_doe"', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.SelectedRequest.QueryParameters[0].Value == "john_doe"');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.SelectedRequest.PostParameters.Count == 2', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.SelectedRequest.PostParameters.Count == 2');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.SelectedRequest.PostParameters[0].Key == "client"', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.SelectedRequest.PostParameters[0].Key == "client"');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.SelectedRequest.PostParameters[0].Value == "desktop"', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Network.SelectedRequest.PostParameters[0].Value == "desktop"');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
