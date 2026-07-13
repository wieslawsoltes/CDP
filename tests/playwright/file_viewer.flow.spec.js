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

    await test.step('Tap on element #TabSources', async () => {
      const element_0 = page.locator('#TabSources');
      await element_0.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: document.querySelector(\'#TabSources\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#TabSources\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert element #treeWorkspaceFiles is visible', async () => {
      await expect(page.locator('#treeWorkspaceFiles')).toBeVisible();
    });

    await test.step('Evaluate Script: var vm = (CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext; var firstFile = vm.Sources.WorkspaceFiles.FirstOrDefault(f => !f.IsDirectory); if (firstFile != null) { vm.Sources.SelectedFile = firstFile; }', async () => {
      await page.evaluate('var vm = (CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext; var firstFile = vm.Sources.WorkspaceFiles.FirstOrDefault(f => !f.IsDirectory); if (firstFile != null) { vm.Sources.SelectedFile = firstFile; }');
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert element #pnlCodeViewer is visible', async () => {
      await expect(page.locator('#pnlCodeViewer')).toBeVisible();
    });

    await test.step('Assert element #lblSourceFileName is visible', async () => {
      await expect(page.locator('#lblSourceFileName')).toBeVisible();
    });

    await test.step('Assert element #txtSourceContent is visible', async () => {
      await expect(page.locator('#txtSourceContent')).toBeVisible();
    });

    await browser.close();
  });
});
