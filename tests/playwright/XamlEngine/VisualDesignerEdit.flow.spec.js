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

    await test.step('Tap on element #TabElements', async () => {
      const element_0 = page.locator('#TabElements');
      await element_0.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: document.querySelector(\'#TabElements\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#TabElements\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #treeDom', async () => {
      const element_3 = page.locator('#treeDom');
      await element_3.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Type text in element #txtAttrName', async () => {
      const element_5 = page.locator('#txtAttrName');
      await element_5.fill('Width');
    });

    await test.step('Type text in element #txtAttrValue', async () => {
      const element_6 = page.locator('#txtAttrValue');
      await element_6.fill('800');
    });

    await test.step('Tap on element #btnApplyAttr', async () => {
      const element_7 = page.locator('#btnApplyAttr');
      await element_7.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Tap on element #TabSources', async () => {
      const element_9 = page.locator('#TabSources');
      await element_9.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: document.querySelector(\'#TabSources\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#TabSources\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #btnSaveFile', async () => {
      const element_12 = page.locator('#btnSaveFile');
      await element_12.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: !Window.DataContext.Sources.IsDirty', async () => {
      const result = await page.evaluate('!Window.DataContext.Sources.IsDirty');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: Window.DataContext.Sources.ActiveEditor.Text.Contains(\'Width="800"\')', async () => {
      const result = await page.evaluate('Window.DataContext.Sources.ActiveEditor.Text.Contains(\'Width="800"\')');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
