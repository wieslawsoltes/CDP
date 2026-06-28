#nullable enable

using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace CdpInspectorApp.Views;

public static class ToolboxMenuHelper
{
    public static readonly DataFormat<string> CdpCommandFormat = DataFormat.CreateInProcessFormat<string>("cdp-command");

    public static ContextMenu CreateContextMenu(Action<string> onCommandSelected)
    {
        var menu = new ContextMenu();
        
        // Interactions Category
        var interactions = new MenuItem { Header = "Interactions" };
        var interactionsCommands = new[]
        {
            ("Tap", "tapOn"),
            ("Double Tap", "doubleTap"),
            ("Long Press", "longPress"),
            ("Input Text", "input"),
            ("Clear", "clear"),
            ("Paste", "paste"),
            ("Erase", "erase"),
            ("Swipe", "swipe"),
            ("Scroll", "scroll"),
            ("Copy", "copy")
        };
        foreach (var (label, cmd) in interactionsCommands)
        {
            var item = new MenuItem { Header = label };
            item.Click += (s, e) => onCommandSelected(cmd);
            interactions.Items.Add(item);
        }
        menu.Items.Add(interactions);

        // Assertions Category
        var assertions = new MenuItem { Header = "Assertions" };
        var assertionsCommands = new[]
        {
            ("Assert Visible", "assertVisible"),
            ("Assert Not Visible", "assertNotVisible"),
            ("Assert True", "assertTrue"),
            ("Assert False", "assertFalse"),
            ("Assert Property...", "assertProperty")
        };
        foreach (var (label, cmd) in assertionsCommands)
        {
            var item = new MenuItem { Header = label };
            item.Click += (s, e) => onCommandSelected(cmd);
            assertions.Items.Add(item);
        }
        menu.Items.Add(assertions);

        // App & Device Category
        var appDevice = new MenuItem { Header = "App & Device" };
        var appDeviceCommands = new[]
        {
            ("Launch", "launchApp"),
            ("Stop", "stopApp"),
            ("Kill", "killApp"),
            ("Clear State", "clearState"),
            ("Orient", "setOrientation"),
            ("Location", "setLocation"),
            ("Airplane", "setAirplaneMode"),
            ("Screenshot", "takeScreenshot"),
            ("Back", "back"),
            ("Delay", "delay"),
            ("Link", "openLink")
        };
        foreach (var (label, cmd) in appDeviceCommands)
        {
            var item = new MenuItem { Header = label };
            item.Click += (s, e) => onCommandSelected(cmd);
            appDevice.Items.Add(item);
        }
        menu.Items.Add(appDevice);

        // Logic Category
        var logic = new MenuItem { Header = "Logic" };
        var logicCommands = new[]
        {
            ("Repeat", "repeat"),
            ("Retry", "retry"),
            ("Run Flow", "runFlow"),
            ("Eval Script", "evalScript")
        };
        foreach (var (label, cmd) in logicCommands)
        {
            var item = new MenuItem { Header = label };
            item.Click += (s, e) => onCommandSelected(cmd);
            logic.Items.Add(item);
        }
        menu.Items.Add(logic);

        return menu;
    }

    public static string GetYamlTemplateForCommand(string command)
    {
        return command switch
        {
            "tapOn" => "- tapOn: \"\"\n",
            "doubleTap" => "- doubleTapOn: \"\"\n",
            "longPress" => "- longPressOn: \"\"\n",
            "input" => "- inputText:\n    selector: \"\"\n    text: \"\"\n",
            "clear" => "- clearText: \"\"\n",
            "paste" => "- pasteText: \"\"\n",
            "erase" => "- eraseText: 1\n",
            "swipe" => "- swipe: \"0,0,100,100\"\n",
            "scroll" => "- scroll:\n    selector: \"\"\n    direction: \"down\"\n",
            "copy" => "- copyTextFrom: \"\"\n",
            "assertVisible" => "- assertVisible: \"\"\n",
            "assertNotVisible" => "- assertNotVisible: \"\"\n",
            "assertTrue" => "- assertTrue: \"\"\n",
            "assertFalse" => "- assertFalse: \"\"\n",
            "assertProperty" => "- assertProperty:\n    selector: \"\"\n    value: \"text == expected_value\"\n",
            "launchApp" => "- launchApp: \"\"\n",
            "stopApp" => "- stopApp: \"\"\n",
            "killApp" => "- killApp: \"\"\n",
            "clearState" => "- clearState: \"\"\n",
            "setOrientation" => "- setOrientation: \"portrait\"\n",
            "setLocation" => "- setLocation: \"37.7749,-122.4194\"\n",
            "setAirplaneMode" => "- setAirplaneMode: \"true\"\n",
            "takeScreenshot" => "- takeScreenshot: \"\"\n",
            "back" => "- back: \"\"\n",
            "delay" => "- delay: 1000\n",
            "openLink" => "- openLink: \"https://\"\n",
            "repeat" => "- repeat:\n    times: 5\n    commands:\n      - tapOn: \"\"\n",
            "retry" => "- retry:\n    maxRetries: 3\n    commands:\n      - tapOn: \"\"\n",
            "runFlow" => "- runFlow: \"other_flow.yaml\"\n",
            "evalScript" => "- evalScript: \"1 == 1\"\n",
            _ => $"- {command}: \"\"\n"
        };
    }

    public static string? MapContentToCommand(string content)
    {
        return content switch
        {
            "Tap" => "tapOn",
            "Double Tap" => "doubleTap",
            "Long Press" => "longPress",
            "Input Text" => "input",
            "Clear" => "clear",
            "Paste" => "paste",
            "Erase" => "erase",
            "Swipe" => "swipe",
            "Scroll" => "scroll",
            "Copy" => "copy",
            "Assert Visible" => "assertVisible",
            "Assert Not Visible" => "assertNotVisible",
            "Assert True" => "assertTrue",
            "Assert False" => "assertFalse",
            "Assert Property..." => "assertProperty",
            "Launch" => "launchApp",
            "Stop" => "stopApp",
            "Kill" => "killApp",
            "Clear State" => "clearState",
            "Orient" => "setOrientation",
            "Location" => "setLocation",
            "Airplane" => "setAirplaneMode",
            "Screenshot" => "takeScreenshot",
            "Back" => "back",
            "Delay" => "delay",
            "Link" => "openLink",
            "Repeat" => "repeat",
            "Retry" => "retry",
            "Run Flow" => "runFlow",
            "Eval Script" => "evalScript",
            _ => null
        };
    }
}
