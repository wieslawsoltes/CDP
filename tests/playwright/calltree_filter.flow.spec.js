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

    await test.step('Assert True: Window.DataContext.Profiler.SearchText == null', async () => {
      const result = await page.evaluate('Window.DataContext.Profiler.SearchText == null');
      await expect(result).toBeTruthy();
    });

    await test.step('Type text in element #txtProfilerSearch', async () => {
      const element_2 = page.locator('#txtProfilerSearch');
      await element_2.fill('Main');
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: Window.DataContext.Profiler.SearchText == "Main"', async () => {
      const result = await page.evaluate('Window.DataContext.Profiler.SearchText == "Main"');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
