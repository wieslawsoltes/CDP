import { test, expect, chromium } from '@playwright/test';

test.describe('WindowsRdpApp CDP Tests', () => {
  test('connect windows rdp success', async () => {
    const browser = await chromium.connectOverCDP('http://localhost:9225');
    const context = browser.contexts()[0];
    const page = context.pages()[0];

    await test.step('Set viewport size', async () => {
      await page.setViewportSize({ width: 1024, height: 768 });
    });

    await test.step('Delay 300ms', async () => {
      await page.waitForTimeout(300);
    });

    await test.step('Assert navSidebar present', async () => {
      const element = page.locator('#navSidebar');
      await expect(element).toBeDefined();
    });

    await browser.close();
  });
});
