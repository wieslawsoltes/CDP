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

    await test.step('Tap on element #tabGestures', async () => {
      const element_1 = page.locator('#tabGestures');
      await element_1.tap();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: document.querySelector(\'#DoubleClickStatus\').textContent == "Not Double Clicked"', async () => {
      const result = await page.evaluate('document.querySelector(\'#DoubleClickStatus\').textContent == "Not Double Clicked"');
      await expect(result).toBeTruthy();
    });

    await test.step('Double tap on element #btnDoubleClick', async () => {
      const element_4 = page.locator('#btnDoubleClick');
      await element_4.dblclick();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: document.querySelector(\'#DoubleClickStatus\').textContent != "Not Double Clicked"', async () => {
      const result = await page.evaluate('document.querySelector(\'#DoubleClickStatus\').textContent != "Not Double Clicked"');
      await expect(result).toBeTruthy();
    });

    await test.step('Long press on element #btnLongPress', async () => {
      const element_7 = page.locator('#btnLongPress');
      await element_7.click({ delay: 1000 });
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: document.querySelector(\'#LongPressStatus\').textContent != "Not Long Pressed"', async () => {
      const result = await page.evaluate('document.querySelector(\'#LongPressStatus\').textContent != "Not Long Pressed"');
      await expect(result).toBeTruthy();
    });

    await test.step('Clear text in element #txtClearTarget', async () => {
      const element_10 = page.locator('#txtClearTarget');
      await element_10.clear();
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: document.querySelector(\'#txtClearTarget\').value == ""', async () => {
      const result = await page.evaluate('document.querySelector(\'#txtClearTarget\').value == ""');
      await expect(result).toBeTruthy();
    });

    await test.step('Drag element #borderDragSource to #borderDropTarget', async () => {
      const source_13 = page.locator('#borderDragSource');
      const target_13 = page.locator('#borderDropTarget');
      await source_13.dragTo(target_13);
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: document.querySelector(\'#DragDropStatus\').textContent == "Dropped Successfully!"', async () => {
      const result = await page.evaluate('document.querySelector(\'#DragDropStatus\').textContent == "Dropped Successfully!"');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
