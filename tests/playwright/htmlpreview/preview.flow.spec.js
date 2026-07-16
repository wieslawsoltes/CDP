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

    await test.step('Delay 300ms', async () => {
      await page.waitForTimeout(300);
    });

    await test.step('Tap on element #btnRefreshTargets', async () => {
      const element_1 = page.locator('#btnRefreshTargets');
      await element_1.tap();
    });

    await test.step('Delay 300ms', async () => {
      await page.waitForTimeout(300);
    });

    await test.step('Tap on element #btnConnect', async () => {
      const element_3 = page.locator('#btnConnect');
      await element_3.tap();
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Assert True: __raw_window.DataContext.Connection.IsConnected', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Connection.IsConnected');
      await expect(result).toBeTruthy();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Tap on element #TabHtmlPreview', async () => {
      const element_7 = page.locator('#TabHtmlPreview');
      await element_7.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: Window.DataContext.HtmlPreview != null', async () => {
      const result = await page.evaluate('Window.DataContext.HtmlPreview != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: !Window.DataContext.HtmlPreview.IsCustomMode', async () => {
      const result = await page.evaluate('!Window.DataContext.HtmlPreview.IsCustomMode');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #chkCustomMode', async () => {
      const element_11 = page.locator('#chkCustomMode');
      await element_11.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: Window.DataContext.HtmlPreview.IsCustomMode', async () => {
      const result = await page.evaluate('Window.DataContext.HtmlPreview.IsCustomMode');
      await expect(result).toBeTruthy();
    });

    await test.step('Type text in element #txtCustomHtml', async () => {
      const element_14 = page.locator('#txtCustomHtml');
      await element_14.fill('<div class="test">E2E Test Output</div>');
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: Window.DataContext.HtmlPreview.HtmlText.Contains("E2E Test Output")', async () => {
      const result = await page.evaluate('Window.DataContext.HtmlPreview.HtmlText.Contains("E2E Test Output")');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
