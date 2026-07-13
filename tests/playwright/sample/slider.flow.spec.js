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

    await test.step('Assert element #sliderValue is visible', async () => {
      await expect(page.locator('#sliderValue')).toBeVisible();
    });

    await test.step('Assert element #txtSliderVal is visible', async () => {
      await expect(page.locator('#txtSliderVal')).toBeVisible();
    });

    await test.step('Assert element #progressVal is visible', async () => {
      await expect(page.locator('#progressVal')).toBeVisible();
    });

    await test.step('Assert True: document.querySelector(\'#sliderValue\').value == 50', async () => {
      const result = await page.evaluate('document.querySelector(\'#sliderValue\').value == 50');
      await expect(result).toBeTruthy();
    });

    await test.step('Evaluate Script: document.querySelector(\'#sliderValue\').value = 75', async () => {
      await page.evaluate('document.querySelector(\'#sliderValue\').value = 75');
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: document.querySelector(\'#sliderValue\').value == 75', async () => {
      const result = await page.evaluate('document.querySelector(\'#sliderValue\').value == 75');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: document.querySelector(\'#progressVal\').value == 75', async () => {
      const result = await page.evaluate('document.querySelector(\'#progressVal\').value == 75');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: document.querySelector(\'#txtSliderVal\').textContent == \'Slider Value: 75\'', async () => {
      const result = await page.evaluate('document.querySelector(\'#txtSliderVal\').textContent == \'Slider Value: 75\'');
      await expect(result).toBeTruthy();
    });

    await test.step('Evaluate Script: document.querySelector(\'#sliderValue\').value = 25', async () => {
      await page.evaluate('document.querySelector(\'#sliderValue\').value = 25');
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: document.querySelector(\'#sliderValue\').value == 25', async () => {
      const result = await page.evaluate('document.querySelector(\'#sliderValue\').value == 25');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: document.querySelector(\'#progressVal\').value == 25', async () => {
      const result = await page.evaluate('document.querySelector(\'#progressVal\').value == 25');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: document.querySelector(\'#txtSliderVal\').textContent == \'Slider Value: 25\'', async () => {
      const result = await page.evaluate('document.querySelector(\'#txtSliderVal\').textContent == \'Slider Value: 25\'');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
