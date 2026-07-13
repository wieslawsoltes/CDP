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

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Tap on element #tabScroll', async () => {
      const element_1 = page.locator('#tabScroll');
      await element_1.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: document.querySelector(\'#scrollContainer\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#scrollContainer\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: Query(\'#scrollContainer\').Offset.Y == 0', async () => {
      const result = await page.evaluate('Query(\'#scrollContainer\').Offset.Y == 0');
      await expect(result).toBeTruthy();
    });

    await test.step('Scroll element or page', async () => {
      const element_5 = page.locator('#scrollContainer');
      await element_5.evaluate(el => {
        let parent = el;
        while (parent) {
          if (parent.scrollHeight > parent.clientHeight && window.getComputedStyle(parent).overflowY !== 'visible') {
            parent.scrollBy(-0, -0);
            return;
          }
          parent = parent.parentElement;
        }
        window.scrollBy(-0, -0);
      });
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: Query(\'#scrollContainer\').Offset.Y > 0', async () => {
      const result = await page.evaluate('Query(\'#scrollContainer\').Offset.Y > 0');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
