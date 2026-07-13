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

    await test.step('Assert element #lstNetworkRequests is visible', async () => {
      await expect(page.locator('#lstNetworkRequests')).toBeVisible();
    });

    // Warning: Unsupported step type 'evalScript'

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Tap on element TextBlock:has-text(\'Payload\')', async () => {
      const element_4 = page.locator('TextBlock:has-text(\'Payload\')');
      await element_4.tap();
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
