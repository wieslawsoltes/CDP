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

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Tap on element #TabConsole', async () => {
      const element_7 = page.locator('#TabConsole');
      await element_7.tap();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Assert True: document.querySelector(\'#TabConsole\') != null', async () => {
      const result = await page.evaluate('document.querySelector(\'#TabConsole\') != null');
      await expect(result).toBeTruthy();
    });

    await test.step('Delay 500ms', async () => {
      await page.waitForTimeout(500);
    });

    await test.step('Evaluate Script: Window.DataContext.Console.ConsoleInputText = "document.querySelector(\'#tabContainer\').selectedIndex = 5"; Window.DataContext.Console.EvaluateAsync();', async () => {
      await page.evaluate('Window.DataContext.Console.ConsoleInputText = "document.querySelector(\'#tabContainer\').selectedIndex = 5"; Window.DataContext.Console.EvaluateAsync();');
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Evaluate Script: Window.DataContext.Console.ConsoleInputText = "document.querySelector(\'#comboPopup\').IsDropDownOpen = true; \'Opened\'"; Window.DataContext.Console.EvaluateAsync();', async () => {
      await page.evaluate('Window.DataContext.Console.ConsoleInputText = "document.querySelector(\'#comboPopup\').IsDropDownOpen = true; \'Opened\'"; Window.DataContext.Console.EvaluateAsync();');
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Evaluate Script: Window.DataContext.Console.ConsoleInputText = "document.querySelector(\'#comboItem2\') != null"; Window.DataContext.Console.EvaluateAsync();', async () => {
      await page.evaluate('Window.DataContext.Console.ConsoleInputText = "document.querySelector(\'#comboItem2\') != null"; Window.DataContext.Console.EvaluateAsync();');
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Assert True: Window.DataContext.Console.ConsoleHistory[Window.DataContext.Console.ConsoleHistory.Count - 1].Result == \'true\'', async () => {
      const result = await page.evaluate('Window.DataContext.Console.ConsoleHistory[Window.DataContext.Console.ConsoleHistory.Count - 1].Result == \'true\'');
      await expect(result).toBeTruthy();
    });

    await test.step('Evaluate Script: Window.DataContext.Elements.RefreshDomTreeAsync();', async () => {
      await page.evaluate('Window.DataContext.Elements.RefreshDomTreeAsync();');
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Evaluate Script: var res = (function() {\n    function find(nodes) {\n        for (var i = 0; i < nodes.Count; i++) {\n            var n = nodes[i];\n            for (var j = 0; j < n.AttributesList.Count; j++) {\n                var a = n.AttributesList[j];\n                if ((a.Name === \'id\' || a.Name === \'Name\') && a.Value === \'comboItem2\') return true;\n            }\n            if (find(n.Children)) return true;\n        }\n        return false;\n    }\n    return find(Window.DataContext.Elements.RootNodes);\n})();\nPrint("FIND comboItem2 RESULT: " + res);\nWindow.DataContext.Console.ConsoleInputText = res ? "true" : "false";\n', async () => {
      await page.evaluate('var res = (function() {\n    function find(nodes) {\n        for (var i = 0; i < nodes.Count; i++) {\n            var n = nodes[i];\n            for (var j = 0; j < n.AttributesList.Count; j++) {\n                var a = n.AttributesList[j];\n                if ((a.Name === \'id\' || a.Name === \'Name\') && a.Value === \'comboItem2\') return true;\n            }\n            if (find(n.Children)) return true;\n        }\n        return false;\n    }\n    return find(Window.DataContext.Elements.RootNodes);\n})();\nPrint("FIND comboItem2 RESULT: " + res);\nWindow.DataContext.Console.ConsoleInputText = res ? "true" : "false";\n');
    });

    await test.step('Assert True: Window.DataContext.Console.ConsoleInputText == \'true\'', async () => {
      const result = await page.evaluate('Window.DataContext.Console.ConsoleInputText == \'true\'');
      await expect(result).toBeTruthy();
    });

    await test.step('Evaluate Script: Window.DataContext.Console.ConsoleInputText = "document.querySelector(\'#comboPopup\').selectedIndex = 1; \'Selected\'"; Window.DataContext.Console.EvaluateAsync();', async () => {
      await page.evaluate('Window.DataContext.Console.ConsoleInputText = "document.querySelector(\'#comboPopup\').selectedIndex = 1; \'Selected\'"; Window.DataContext.Console.EvaluateAsync();');
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Evaluate Script: Window.DataContext.Console.ConsoleInputText = "document.querySelector(\'#comboPopup\').__raw_node.SelectedItem.Content"; Window.DataContext.Console.EvaluateAsync();', async () => {
      await page.evaluate('Window.DataContext.Console.ConsoleInputText = "document.querySelector(\'#comboPopup\').__raw_node.SelectedItem.Content"; Window.DataContext.Console.EvaluateAsync();');
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Assert True: Window.DataContext.Console.ConsoleHistory[Window.DataContext.Console.ConsoleHistory.Count - 1].Result == \'Popup Option 2\'', async () => {
      const result = await page.evaluate('Window.DataContext.Console.ConsoleHistory[Window.DataContext.Console.ConsoleHistory.Count - 1].Result == \'Popup Option 2\'');
      await expect(result).toBeTruthy();
    });

    await test.step('Evaluate Script: Window.DataContext.Console.ConsoleInputText = "var btn = document.querySelector(\'#btnContextMenu\').__raw_node; btn.ContextMenu.Open(btn); \'Opened\'"; Window.DataContext.Console.EvaluateAsync();', async () => {
      await page.evaluate('Window.DataContext.Console.ConsoleInputText = "var btn = document.querySelector(\'#btnContextMenu\').__raw_node; btn.ContextMenu.Open(btn); \'Opened\'"; Window.DataContext.Console.EvaluateAsync();');
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Evaluate Script: Window.DataContext.Elements.RefreshDomTreeAsync();', async () => {
      await page.evaluate('Window.DataContext.Elements.RefreshDomTreeAsync();');
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Evaluate Script: var res = (function() {\n    function find(nodes) {\n        for (var i = 0; i < nodes.Count; i++) {\n            var n = nodes[i];\n            for (var j = 0; j < n.AttributesList.Count; j++) {\n                var a = n.AttributesList[j];\n                if ((a.Name === \'id\' || a.Name === \'Name\') && a.Value === \'menuItem1\') return true;\n            }\n            if (find(n.Children)) return true;\n        }\n        return false;\n    }\n    return find(Window.DataContext.Elements.RootNodes);\n})();\nPrint("FIND menuItem1 RESULT: " + res);\nWindow.DataContext.Console.ConsoleInputText = res ? "true" : "false";\n', async () => {
      await page.evaluate('var res = (function() {\n    function find(nodes) {\n        for (var i = 0; i < nodes.Count; i++) {\n            var n = nodes[i];\n            for (var j = 0; j < n.AttributesList.Count; j++) {\n                var a = n.AttributesList[j];\n                if ((a.Name === \'id\' || a.Name === \'Name\') && a.Value === \'menuItem1\') return true;\n            }\n            if (find(n.Children)) return true;\n        }\n        return false;\n    }\n    return find(Window.DataContext.Elements.RootNodes);\n})();\nPrint("FIND menuItem1 RESULT: " + res);\nWindow.DataContext.Console.ConsoleInputText = res ? "true" : "false";\n');
    });

    await test.step('Assert True: Window.DataContext.Console.ConsoleInputText == \'true\'', async () => {
      const result = await page.evaluate('Window.DataContext.Console.ConsoleInputText == \'true\'');
      await expect(result).toBeTruthy();
    });

    await test.step('Evaluate Script: Window.DataContext.Console.ConsoleInputText = "var btn = document.querySelector(\'#btnContextMenu\').__raw_node; btn.ContextMenu.Open(btn); \'Opened\'";\nWindow.DataContext.Console.EvaluateAsync();\n', async () => {
      await page.evaluate('Window.DataContext.Console.ConsoleInputText = "var btn = document.querySelector(\'#btnContextMenu\').__raw_node; btn.ContextMenu.Open(btn); \'Opened\'";\nWindow.DataContext.Console.EvaluateAsync();\n');
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Evaluate Script: Window.DataContext.Console.ConsoleInputText = "var btn = document.querySelector(\'#btnContextMenu\').__raw_node; var cm = btn.ContextMenu; var item = __getChildren(cm)[0]; Window.MenuItem_Click(item, null); \'Clicked\'";\nWindow.DataContext.Console.EvaluateAsync();\n', async () => {
      await page.evaluate('Window.DataContext.Console.ConsoleInputText = "var btn = document.querySelector(\'#btnContextMenu\').__raw_node; var cm = btn.ContextMenu; var item = __getChildren(cm)[0]; Window.MenuItem_Click(item, null); \'Clicked\'";\nWindow.DataContext.Console.EvaluateAsync();\n');
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Evaluate Script: Window.DataContext.Console.ConsoleInputText = "document.querySelector(\'#txtPopupStatus\').__raw_node.Text"; Window.DataContext.Console.EvaluateAsync();', async () => {
      await page.evaluate('Window.DataContext.Console.ConsoleInputText = "document.querySelector(\'#txtPopupStatus\').__raw_node.Text"; Window.DataContext.Console.EvaluateAsync();');
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Assert True: Window.DataContext.Console.ConsoleHistory[Window.DataContext.Console.ConsoleHistory.Count - 2].Result == \'Clicked\'', async () => {
      const result = await page.evaluate('Window.DataContext.Console.ConsoleHistory[Window.DataContext.Console.ConsoleHistory.Count - 2].Result == \'Clicked\'');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: Window.DataContext.Console.ConsoleHistory[Window.DataContext.Console.ConsoleHistory.Count - 1].Result == \'Selected Menu: Menu Item 1\'', async () => {
      const result = await page.evaluate('Window.DataContext.Console.ConsoleHistory[Window.DataContext.Console.ConsoleHistory.Count - 1].Result == \'Selected Menu: Menu Item 1\'');
      await expect(result).toBeTruthy();
    });

    await test.step('Evaluate Script: Window.DataContext.Console.ConsoleInputText = "var btn = document.querySelector(\'#btnFlyout\').__raw_node; btn.Flyout.Show(btn); \'Opened\'"; Window.DataContext.Console.EvaluateAsync();', async () => {
      await page.evaluate('Window.DataContext.Console.ConsoleInputText = "var btn = document.querySelector(\'#btnFlyout\').__raw_node; btn.Flyout.Show(btn); \'Opened\'"; Window.DataContext.Console.EvaluateAsync();');
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Evaluate Script: Window.DataContext.Elements.RefreshDomTreeAsync();', async () => {
      await page.evaluate('Window.DataContext.Elements.RefreshDomTreeAsync();');
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Evaluate Script: var res = (function() {\n    function find(nodes) {\n        for (var i = 0; i < nodes.Count; i++) {\n            var n = nodes[i];\n            for (var j = 0; j < n.AttributesList.Count; j++) {\n                var a = n.AttributesList[j];\n                if ((a.Name === \'id\' || a.Name === \'Name\') && a.Value === \'btnInsideFlyout\') return true;\n            }\n            if (find(n.Children)) return true;\n        }\n        return false;\n    }\n    return find(Window.DataContext.Elements.RootNodes);\n})();\nPrint("FIND btnInsideFlyout RESULT: " + res);\nWindow.DataContext.Console.ConsoleInputText = res ? "true" : "false";\n', async () => {
      await page.evaluate('var res = (function() {\n    function find(nodes) {\n        for (var i = 0; i < nodes.Count; i++) {\n            var n = nodes[i];\n            for (var j = 0; j < n.AttributesList.Count; j++) {\n                var a = n.AttributesList[j];\n                if ((a.Name === \'id\' || a.Name === \'Name\') && a.Value === \'btnInsideFlyout\') return true;\n            }\n            if (find(n.Children)) return true;\n        }\n        return false;\n    }\n    return find(Window.DataContext.Elements.RootNodes);\n})();\nPrint("FIND btnInsideFlyout RESULT: " + res);\nWindow.DataContext.Console.ConsoleInputText = res ? "true" : "false";\n');
    });

    await test.step('Assert True: Window.DataContext.Console.ConsoleInputText == \'true\'', async () => {
      const result = await page.evaluate('Window.DataContext.Console.ConsoleInputText == \'true\'');
      await expect(result).toBeTruthy();
    });

    await test.step('Evaluate Script: Window.DataContext.Console.ConsoleInputText = "var btn = document.querySelector(\'#btnFlyout\').__raw_node; btn.Flyout.Show(btn); \'Opened\'";\nWindow.DataContext.Console.EvaluateAsync();\n', async () => {
      await page.evaluate('Window.DataContext.Console.ConsoleInputText = "var btn = document.querySelector(\'#btnFlyout\').__raw_node; btn.Flyout.Show(btn); \'Opened\'";\nWindow.DataContext.Console.EvaluateAsync();\n');
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Evaluate Script: Window.DataContext.Console.ConsoleInputText = "var btn = document.querySelector(\'#btnFlyout\').__raw_node; var panel = btn.Flyout.Content; var item = __getChildren(panel)[1]; Window.BtnInsideFlyout_Click(item, null); \'Clicked\'";\nWindow.DataContext.Console.EvaluateAsync();\n', async () => {
      await page.evaluate('Window.DataContext.Console.ConsoleInputText = "var btn = document.querySelector(\'#btnFlyout\').__raw_node; var panel = btn.Flyout.Content; var item = __getChildren(panel)[1]; Window.BtnInsideFlyout_Click(item, null); \'Clicked\'";\nWindow.DataContext.Console.EvaluateAsync();\n');
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Evaluate Script: Window.DataContext.Console.ConsoleInputText = "document.querySelector(\'#txtPopupStatus\').__raw_node.Text"; Window.DataContext.Console.EvaluateAsync();', async () => {
      await page.evaluate('Window.DataContext.Console.ConsoleInputText = "document.querySelector(\'#txtPopupStatus\').__raw_node.Text"; Window.DataContext.Console.EvaluateAsync();');
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Assert True: Window.DataContext.Console.ConsoleHistory[Window.DataContext.Console.ConsoleHistory.Count - 2].Result == \'Clicked\'', async () => {
      const result = await page.evaluate('Window.DataContext.Console.ConsoleHistory[Window.DataContext.Console.ConsoleHistory.Count - 2].Result == \'Clicked\'');
      await expect(result).toBeTruthy();
    });

    await test.step('Assert True: Window.DataContext.Console.ConsoleHistory[Window.DataContext.Console.ConsoleHistory.Count - 1].Result == \'Clicked Inside Flyout!\'', async () => {
      const result = await page.evaluate('Window.DataContext.Console.ConsoleHistory[Window.DataContext.Console.ConsoleHistory.Count - 1].Result == \'Clicked Inside Flyout!\'');
      await expect(result).toBeTruthy();
    });

    await test.step('Evaluate Script: Window.DataContext.Console.ConsoleInputText = "document.querySelector(\'#tabContainer\').selectedIndex = 0"; Window.DataContext.Console.EvaluateAsync();', async () => {
      await page.evaluate('Window.DataContext.Console.ConsoleInputText = "document.querySelector(\'#tabContainer\').selectedIndex = 0"; Window.DataContext.Console.EvaluateAsync();');
    });

    await test.step('Delay 1000ms', async () => {
      await page.waitForTimeout(1000);
    });

    await test.step('Evaluate Script: Window.DataContext.Console.ConsoleInputText = "document.querySelector(\'#btnOpenSecond\').__raw_node.Command.Execute(null); \'Opened\'"; Window.DataContext.Console.EvaluateAsync();', async () => {
      await page.evaluate('Window.DataContext.Console.ConsoleInputText = "document.querySelector(\'#btnOpenSecond\').__raw_node.Command.Execute(null); \'Opened\'"; Window.DataContext.Console.EvaluateAsync();');
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Evaluate Script: Window.DataContext.Elements.RefreshDomTreeAsync();', async () => {
      await page.evaluate('Window.DataContext.Elements.RefreshDomTreeAsync();');
    });

    await test.step('Delay 1500ms', async () => {
      await page.waitForTimeout(1500);
    });

    await test.step('Evaluate Script: var res = (function() {\n    function find(nodes) {\n        for (var i = 0; i < nodes.Count; i++) {\n            var n = nodes[i];\n            for (var j = 0; j < n.AttributesList.Count; j++) {\n                var a = n.AttributesList[j];\n                if (a.Name === \'type\' && a.Value === \'Avalonia.Controls.Window\') return true;\n            }\n            if (find(n.Children)) return true;\n        }\n        return false;\n    }\n    return find(Window.DataContext.Elements.RootNodes);\n})();\nPrint("FIND Window RESULT: " + res);\nWindow.DataContext.Console.ConsoleInputText = res ? "true" : "false";\n', async () => {
      await page.evaluate('var res = (function() {\n    function find(nodes) {\n        for (var i = 0; i < nodes.Count; i++) {\n            var n = nodes[i];\n            for (var j = 0; j < n.AttributesList.Count; j++) {\n                var a = n.AttributesList[j];\n                if (a.Name === \'type\' && a.Value === \'Avalonia.Controls.Window\') return true;\n            }\n            if (find(n.Children)) return true;\n        }\n        return false;\n    }\n    return find(Window.DataContext.Elements.RootNodes);\n})();\nPrint("FIND Window RESULT: " + res);\nWindow.DataContext.Console.ConsoleInputText = res ? "true" : "false";\n');
    });

    await test.step('Assert True: Window.DataContext.Console.ConsoleInputText == \'true\'', async () => {
      const result = await page.evaluate('Window.DataContext.Console.ConsoleInputText == \'true\'');
      await expect(result).toBeTruthy();
    });

    await browser.close();
  });
});
