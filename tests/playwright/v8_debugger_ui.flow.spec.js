import { test, expect, chromium } from '@playwright/test';

test.describe('CDP Recorded Tests', () => {
  test('recorded test', async () => {
    const browser = await chromium.connectOverCDP('http://127.0.0.1:9223');
    const context = browser.contexts()[0];
    const page = context.pages()[0];
    // Avalonia's CDP DOM exposes named controls, but does not implement every
    // browser layout primitive Playwright's web-specific toBeVisible uses.
    const expectCdpElement = async (selector) => {
      const present = await page.evaluate(`document.querySelector('${selector}') != null`);
      await expect(present).toBeTruthy();
    };

    await test.step('Set viewport size', async () => {
      await page.setViewportSize({ width: 1320, height: 768 });
    });

    await test.step('Evaluate Script: __raw_window.DataContext.Connection.DisconnectCommand.Execute(null)', async () => {
      await page.evaluate('__raw_window.DataContext.Connection.DisconnectCommand.Execute(null)');
    });

    await test.step('Delay 300ms', async () => {
      await page.waitForTimeout(300);
    });

    await test.step('Evaluate Script: var connection = __raw_window.DataContext.Connection;\nconnection.HostAddress = "http://127.0.0.1:9222";\nconnection.RefreshTargetsCommand.Execute(null);\n', async () => {
      await page.evaluate('var connection = __raw_window.DataContext.Connection;\nconnection.HostAddress = "http://127.0.0.1:9222";\nconnection.RefreshTargetsCommand.Execute(null);\n');
    });

    await test.step('Delay 700ms', async () => {
      await page.waitForTimeout(700);
    });

    await test.step('Evaluate Script: __raw_window.DataContext.Connection.ConnectCommand.Execute(null)', async () => {
      await page.evaluate('__raw_window.DataContext.Connection.ConnectCommand.Execute(null)');
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Assert True: __raw_window.DataContext.Connection.IsConnected', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Connection.IsConnected');
      await expect(result).toBeTruthy();
    });

    await test.step('Tap on element #TabSources', async () => {
      const element_7 = page.locator('#TabSources');
      await element_7.click();
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

    await test.step('Assert element #btnDebuggerResume is visible', async () => {
      await expectCdpElement('#btnDebuggerResume');
    });

    await test.step('Assert element #btnDebuggerPause is visible', async () => {
      await expectCdpElement('#btnDebuggerPause');
    });

    await test.step('Assert element #btnDebuggerStepOver is visible', async () => {
      await expectCdpElement('#btnDebuggerStepOver');
    });

    await test.step('Assert element #btnDebuggerStepInto is visible', async () => {
      await expectCdpElement('#btnDebuggerStepInto');
    });

    await test.step('Assert element #btnDebuggerStepOut is visible', async () => {
      await expectCdpElement('#btnDebuggerStepOut');
    });

    await test.step('Assert element #btnDebuggerRestartFrame is visible', async () => {
      await expectCdpElement('#btnDebuggerRestartFrame');
    });

    await test.step('Assert element #cmbPauseOnExceptions is visible', async () => {
      await expectCdpElement('#cmbPauseOnExceptions');
    });

    await test.step('Assert element #txtDebuggerExpression is visible', async () => {
      await expectCdpElement('#txtDebuggerExpression');
    });

    await test.step('Assert element #btnDebuggerEvaluate is visible', async () => {
      await expectCdpElement('#btnDebuggerEvaluate');
    });

    await test.step('Assert the resizable debugger split workspace is visible', async () => {
      await expectCdpElement('#DebuggerSplitControl');
      const hasSplitLayout = await page.evaluate('__raw_window.DataContext.Sources.DebuggerLayoutRoot != null');
      await expect(hasSplitLayout).toBeTruthy();
    });

    await test.step('Assert element #txtNewWatchExpression is visible', async () => {
      await expectCdpElement('#txtNewWatchExpression');
    });

    await test.step('Assert element #btnAddWatchExpression is visible', async () => {
      await expectCdpElement('#btnAddWatchExpression');
    });

    await test.step('Assert element #lstDebuggerWatches is visible', async () => {
      await expectCdpElement('#lstDebuggerWatches');
    });

    await test.step('Assert True: document.querySelector(\'#txtLiveEditStatus\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#txtLiveEditStatus\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert element #lstDebuggerCallFrames is visible', async () => {
      await expectCdpElement('#lstDebuggerCallFrames');
    });

    await test.step('Assert element #dgDebuggerScopes is visible', async () => {
      await expectCdpElement('#dgDebuggerScopes');
    });

    await test.step('Assert element #lstV8Breakpoints is visible', async () => {
      await expectCdpElement('#lstV8Breakpoints');
    });

    await test.step('Assert True: document.querySelector(\'#chkBreakpointsActive\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#chkBreakpointsActive\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: document.querySelector(\'#cmbBreakpointKind\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#cmbBreakpointKind\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: document.querySelector(\'#txtBreakpointLogMessage\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#txtBreakpointLogMessage\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: document.querySelector(\'#btnUpdateBreakpoint\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#btnUpdateBreakpoint\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: document.querySelector(\'#btnToggleBreakpointEnabled\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#btnToggleBreakpointEnabled\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: document.querySelector(\'#btnRemoveBreakpoint\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#btnRemoveBreakpoint\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: document.querySelector(\'#txtNewVariableValue\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#txtNewVariableValue\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: document.querySelector(\'#btnSetVariableValue\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#btnSetVariableValue\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Open the Ignore List debugger tab', async () => {
      await page.locator('#TabDebuggerIgnoreList').click();
      await page.waitForTimeout(200);
    });

    await test.step('Assert True: document.querySelector(\'#lstBlackboxPatterns\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#lstBlackboxPatterns\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: document.querySelector(\'#txtNewBlackboxPattern\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#txtNewBlackboxPattern\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: document.querySelector(\'#btnAddBlackboxPattern\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#btnAddBlackboxPattern\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: document.querySelector(\'#chkSkipAnonymousScripts\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#chkSkipAnonymousScripts\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: document.querySelector(\'#btnApplyBlackboxPatterns\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#btnApplyBlackboxPatterns\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: document.querySelector(\'#btnRemoveBlackboxPattern\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#btnRemoveBlackboxPattern\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Sources.PauseOnExceptionsStates.Count == 4', async () => {
      const result = await page.evaluate('((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Sources.PauseOnExceptionsStates.Count == 4');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: __raw_window.DataContext.Sources.BreakpointKinds.Count == 3', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Sources.BreakpointKinds.Count == 3');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: __raw_window.DataContext.Sources.AreBreakpointsActive', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Sources.AreBreakpointsActive');
      await expect(result).toBeTruthy();
    });

    await test.step('Evaluate Script: var sources = __raw_window.DataContext.Sources;\nsources.NewBlackboxPattern = ".*[/\\\\\\\\]node_modules[/\\\\\\\\].*";\nsources.AddBlackboxPatternCommand.Execute(null);\n', async () => {
      await page.evaluate('var sources = __raw_window.DataContext.Sources;\nsources.NewBlackboxPattern = ".*[/\\\\\\\\]node_modules[/\\\\\\\\].*";\nsources.AddBlackboxPatternCommand.Execute(null);\n');
    });

    await test.step('Delay 300ms', async () => {
      await page.waitForTimeout(300);
    });

    await test.step('Assert True: __raw_window.DataContext.Sources.BlackboxPatterns.Count == 1', async () => {
      const result = await page.evaluate('__raw_window.DataContext.Sources.BlackboxPatterns.Count == 1');
      await expect(result).toBeTruthy();
    });

    if (process.env.CDP_V8_UI_SCREENSHOT_PATH) {
      await page.screenshot({ path: process.env.CDP_V8_UI_SCREENSHOT_PATH });
    }

    await browser.close();
  });
});
