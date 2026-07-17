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

    await test.step('Tap on element #btnRefreshTargets', async () => {
      const element_1 = page.locator('#btnRefreshTargets');
      await element_1.tap();
    });

    await test.step('Delay 300ms', async () => {
      await page.waitForTimeout(300);
    });

    await test.step('Tap on element #btnConnect', async () => {
      const element_3 = page.locator('#btnConnect');
      await element_3.tap();
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Assert True: __raw_window.DataContext.Connection.IsConnected', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Connection.IsConnected');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #TabSources', async () => {
      const element_6 = page.locator('#TabSources');
      await element_6.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: document.querySelector(\'#TabSources\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#TabSources\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Evaluate Script: var vm = (CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext;\nvar box = vm.FindBoxNodeByViewName(vm.Sources.LayoutRoot, "SourcesFiles");\nif (box != null) {\n    var tab = null;\n    for (var i = 0; i < box.Tabs.Count; i++) {\n        if (box.Tabs[i].SelectedViewName == "SourcesFiles") {\n            tab = box.Tabs[i];\n            break;\n        }\n    }\n    if (tab != null) {\n        box.ActiveTab = tab;\n    }\n}\n', async () => {
      await page.evaluate('var vm = (CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext;\nvar box = vm.FindBoxNodeByViewName(vm.Sources.LayoutRoot, "SourcesFiles");\nif (box != null) {\n    var tab = null;\n    for (var i = 0; i < box.Tabs.Count; i++) {\n        if (box.Tabs[i].SelectedViewName == "SourcesFiles") {\n            tab = box.Tabs[i];\n            break;\n        }\n    }\n    if (tab != null) {\n        box.ActiveTab = tab;\n    }\n}\n');
    });

    await test.step('Delay 200ms', async () => {
      await page.waitForTimeout(200);
    });

    await test.step('Assert element #treeWorkspaceFiles is visible', async () => {
      await expect(page.locator('#treeWorkspaceFiles')).toBeVisible();
    });

    await test.step('Evaluate Script: var vm = Window.DataContext;\nvar markdownFile = null;\nvar files = vm.Sources.WorkspaceFiles;\nfor (var i = 0; i < files.Count; i++) {\n    var f = files[i];\n    if (!f.IsDirectory && f.Name.indexOf(".md") !== -1) {\n        markdownFile = f;\n        break;\n    }\n}\nif (markdownFile != null) {\n    vm.Sources.SelectedFile = markdownFile;\n}\n', async () => {
      await page.evaluate('var vm = Window.DataContext;\nvar markdownFile = null;\nvar files = vm.Sources.WorkspaceFiles;\nfor (var i = 0; i < files.Count; i++) {\n    var f = files[i];\n    if (!f.IsDirectory && f.Name.indexOf(".md") !== -1) {\n        markdownFile = f;\n        break;\n    }\n}\nif (markdownFile != null) {\n    vm.Sources.SelectedFile = markdownFile;\n}\n');
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Evaluate Script: var vm = Window.DataContext;\nvm.Sources.SelectedFileContent = "- [ ] Task item to check\\n- [x] Already checked item";\n', async () => {
      await page.evaluate('var vm = Window.DataContext;\nvm.Sources.SelectedFileContent = "- [ ] Task item to check\\n- [x] Already checked item";\n');
    });

    await test.step('Delay 300ms', async () => {
      await page.waitForTimeout(300);
    });

    await test.step('Assert element #mdVisualEditor is visible', async () => {
      await expect(page.locator('#mdVisualEditor')).toBeVisible();
    });

    await test.step('Evaluate Script: var rect = document.querySelector("#mdVisualEditor").getBoundingClientRect();\nreturn rect.left + 15;\n', async () => {
      await page.evaluate('var rect = document.querySelector("#mdVisualEditor").getBoundingClientRect();\nreturn rect.left + 15;\n');
    });

    await test.step('Evaluate Script: var rect = document.querySelector("#mdVisualEditor").getBoundingClientRect();\nreturn rect.top + 12;\n', async () => {
      await page.evaluate('var rect = document.querySelector("#mdVisualEditor").getBoundingClientRect();\nreturn rect.top + 12;\n');
    });

    await test.step('Tap on element ', async () => {
      const element_19 = page.locator('');
      await element_19.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: Window.DataContext.Sources.SelectedFileContent.indexOf(\'- [x] Task item to check\') !== -1', async () => {
      const result = await page.evaluate('Window.DataContext.Sources.SelectedFileContent.indexOf(\'- [x] Task item to check\') !== -1');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
