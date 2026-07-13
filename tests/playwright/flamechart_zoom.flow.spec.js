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

    await test.step('Tap on element #TabProfiler', async () => {
      const element_0 = page.locator('#TabProfiler');
      await element_0.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: document.querySelector(\'#TabProfiler\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#TabProfiler\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: Window.DataContext.Profiler.ZoomScale == 1.0', async () => {
      const result = await page.evaluate('Window.DataContext.Profiler.ZoomScale == 1.0');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #btnProfilerZoomIn', async () => {
      const element_4 = page.locator('#btnProfilerZoomIn');
      await element_4.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: Window.DataContext.Profiler.ZoomScale > 1.4 && Window.DataContext.Profiler.ZoomScale < 1.6', async () => {
      const result = await page.evaluate('Window.DataContext.Profiler.ZoomScale > 1.4 && Window.DataContext.Profiler.ZoomScale < 1.6');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #btnProfilerZoomOut', async () => {
      const element_7 = page.locator('#btnProfilerZoomOut');
      await element_7.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: Window.DataContext.Profiler.ZoomScale > 0.9 && Window.DataContext.Profiler.ZoomScale < 1.1', async () => {
      const result = await page.evaluate('Window.DataContext.Profiler.ZoomScale > 0.9 && Window.DataContext.Profiler.ZoomScale < 1.1');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
