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

    await test.step('Clear text in element #txtHost', async () => {
      const element_1 = page.locator('#txtHost');
      await element_1.clear();
    });

    await test.step('Type text in element #txtHost', async () => {
      const element_2 = page.locator('#txtHost');
      await element_2.fill('rdp.example.com');
    });

    await test.step('Clear text in element #txtPort', async () => {
      const element_3 = page.locator('#txtPort');
      await element_3.clear();
    });

    await test.step('Type text in element #txtPort', async () => {
      const element_4 = page.locator('#txtPort');
      await element_4.fill('3389');
    });

    await test.step('Type text in element #txtUsername', async () => {
      const element_5 = page.locator('#txtUsername');
      await element_5.fill('administrator');
    });

    await test.step('Tap on element #btnConnect', async () => {
      const element_6 = page.locator('#btnConnect');
      await element_6.tap();
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Assert True: __raw_window.DataContext.Connection.IsConnected == true', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Connection.IsConnected == true');
      await expect(result).toBeTruthy();
    });

    // Warning: Unsupported step type 'assertScreenshot'

    await test.step('Delay 200ms', async () => {
      await page.waitForTimeout(200);
    });

    await test.step('Tap on element #btnDisconnect', async () => {
      const element_11 = page.locator('#btnDisconnect');
      await element_11.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: __raw_window.DataContext.Connection.IsConnected == false', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Connection.IsConnected == false');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
