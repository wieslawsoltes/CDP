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

    await test.step('Evaluate Script: var vm = Window.DataContext;\nvm.Sources.SelectedFileContent = "# Header\\nInitial text.";\n', async () => {
      await page.evaluate('var vm = Window.DataContext;\nvm.Sources.SelectedFileContent = "# Header\\nInitial text.";\n');
    });

    await test.step('Delay 300ms', async () => {
      await page.waitForTimeout(300);
    });

    await test.step('Assert element #btnToggleMarkdownMode is visible', async () => {
      await expect(page.locator('#btnToggleMarkdownMode')).toBeVisible();
    });

    await test.step('Assert element #mdVisualEditor is visible', async () => {
      await expect(page.locator('#mdVisualEditor')).toBeVisible();
    });

    await test.step('Type text in element #mdVisualEditor', async () => {
      const element_18 = page.locator('#mdVisualEditor');
      await element_18.fill('\n## Sub-Header Added');
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Assert True: Window.DataContext.Sources.SelectedFileContent.indexOf(\'Sub-Header Added\') !== -1', async () => {
      const result = await page.evaluate('Window.DataContext.Sources.SelectedFileContent.indexOf(\'Sub-Header Added\') !== -1');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: document.querySelector(\'#mdVisualEditor\').value.indexOf(\'Sub-Header Added\') !== -1', async () => {
      const result = await page.evaluate('document.querySelector(\'#mdVisualEditor\').value.indexOf(\'Sub-Header Added\') !== -1');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: document.querySelector(\'#txtSourceContent\').value.indexOf(\'Sub-Header Added\') !== -1', async () => {
      const result = await page.evaluate('document.querySelector(\'#txtSourceContent\').value.indexOf(\'Sub-Header Added\') !== -1');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
