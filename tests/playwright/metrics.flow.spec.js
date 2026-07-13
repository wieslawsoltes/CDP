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

    await test.step('Assert element #PerformanceChartPanel is visible', async () => {
      await expect(page.locator('#PerformanceChartPanel')).toBeVisible();
    });

    await test.step('Tap on element #btnRefreshMetrics', async () => {
      const element_2 = page.locator('#btnRefreshMetrics');
      await element_2.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Tap on element #btnCollectGarbage', async () => {
      const element_4 = page.locator('#btnCollectGarbage');
      await element_4.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Tap on element #btnStartProfiler', async () => {
      const element_6 = page.locator('#btnStartProfiler');
      await element_6.tap();
    });

    await test.step('Delay 2000ms', async () => {
      await page.waitForTimeout(2000);
    });

    await test.step('Tap on element #btnStopProfiler', async () => {
      const element_8 = page.locator('#btnStopProfiler');
      await element_8.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert element #lblPerfMemory is visible', async () => {
      await expect(page.locator('#lblPerfMemory')).toBeVisible();
    });

    await test.step('Assert True: true', async () => {
      const result = await page.evaluate('true');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
