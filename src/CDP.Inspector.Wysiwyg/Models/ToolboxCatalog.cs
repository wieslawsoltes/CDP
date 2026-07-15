using System.Collections.Generic;

namespace CDP.Inspector.Wysiwyg.Models;

/// <summary>
/// Provides categorized lists of UI elements for each target platform.
/// Supports Avalonia, WPF, WinUI/Uno, and HTML target types.
/// </summary>
public static class ToolboxCatalog
{
    public static IReadOnlyList<ToolboxItem> GetAvaloniaControls() => new[]
    {
        // Layout
        new ToolboxItem("StackPanel", "Layout", "Avalonia", "<StackPanel Orientation=\"Vertical\" />"),
        new ToolboxItem("Grid", "Layout", "Avalonia", "<Grid />"),
        new ToolboxItem("DockPanel", "Layout", "Avalonia", "<DockPanel />"),
        new ToolboxItem("WrapPanel", "Layout", "Avalonia", "<WrapPanel />"),
        new ToolboxItem("Canvas", "Layout", "Avalonia", "<Canvas />"),
        new ToolboxItem("Border", "Layout", "Avalonia", "<Border BorderBrush=\"Gray\" BorderThickness=\"1\" />"),
        new ToolboxItem("ScrollViewer", "Layout", "Avalonia", "<ScrollViewer />"),
        // Controls
        new ToolboxItem("Button", "Controls", "Avalonia", "<Button Content=\"Button\" />"),
        new ToolboxItem("TextBlock", "Controls", "Avalonia", "<TextBlock Text=\"Text\" />"),
        new ToolboxItem("TextBox", "Controls", "Avalonia", "<TextBox />"),
        new ToolboxItem("CheckBox", "Controls", "Avalonia", "<CheckBox Content=\"Check\" />"),
        new ToolboxItem("RadioButton", "Controls", "Avalonia", "<RadioButton Content=\"Radio\" />"),
        new ToolboxItem("ComboBox", "Controls", "Avalonia", "<ComboBox />"),
        new ToolboxItem("Slider", "Controls", "Avalonia", "<Slider Minimum=\"0\" Maximum=\"100\" />"),
        new ToolboxItem("ProgressBar", "Controls", "Avalonia", "<ProgressBar Value=\"50\" Maximum=\"100\" />"),
        new ToolboxItem("Image", "Controls", "Avalonia", "<Image />"),
        new ToolboxItem("ToggleSwitch", "Controls", "Avalonia", "<ToggleSwitch />"),
        // Data
        new ToolboxItem("ListBox", "Data", "Avalonia", "<ListBox />"),
        new ToolboxItem("DataGrid", "Data", "Avalonia", "<DataGrid />"),
        new ToolboxItem("TreeView", "Data", "Avalonia", "<TreeView />"),
        // Navigation
        new ToolboxItem("TabControl", "Navigation", "Avalonia", "<TabControl />"),
        new ToolboxItem("Expander", "Navigation", "Avalonia", "<Expander Header=\"Expander\" />"),
        new ToolboxItem("SplitView", "Navigation", "Avalonia", "<SplitView />"),
    };

    public static IReadOnlyList<ToolboxItem> GetWpfControls() => new[]
    {
        new ToolboxItem("StackPanel", "Layout", "WPF", "<StackPanel Orientation=\"Vertical\" />"),
        new ToolboxItem("Grid", "Layout", "WPF", "<Grid />"),
        new ToolboxItem("DockPanel", "Layout", "WPF", "<DockPanel />"),
        new ToolboxItem("WrapPanel", "Layout", "WPF", "<WrapPanel />"),
        new ToolboxItem("Canvas", "Layout", "WPF", "<Canvas />"),
        new ToolboxItem("Border", "Layout", "WPF", "<Border />"),
        new ToolboxItem("Button", "Controls", "WPF", "<Button Content=\"Button\" />"),
        new ToolboxItem("TextBlock", "Controls", "WPF", "<TextBlock Text=\"Text\" />"),
        new ToolboxItem("TextBox", "Controls", "WPF", "<TextBox />"),
        new ToolboxItem("CheckBox", "Controls", "WPF", "<CheckBox Content=\"Check\" />"),
        new ToolboxItem("RadioButton", "Controls", "WPF", "<RadioButton Content=\"Radio\" />"),
        new ToolboxItem("ComboBox", "Controls", "WPF", "<ComboBox />"),
        new ToolboxItem("Slider", "Controls", "WPF", "<Slider />"),
        new ToolboxItem("ListView", "Data", "WPF", "<ListView />"),
        new ToolboxItem("DataGrid", "Data", "WPF", "<DataGrid />"),
        new ToolboxItem("TreeView", "Data", "WPF", "<TreeView />"),
        new ToolboxItem("TabControl", "Navigation", "WPF", "<TabControl />"),
    };

    public static IReadOnlyList<ToolboxItem> GetWinUiControls() => new[]
    {
        new ToolboxItem("StackPanel", "Layout", "WinUI", "<StackPanel Orientation=\"Vertical\" />"),
        new ToolboxItem("Grid", "Layout", "WinUI", "<Grid />"),
        new ToolboxItem("RelativePanel", "Layout", "WinUI", "<RelativePanel />"),
        new ToolboxItem("Border", "Layout", "WinUI", "<Border />"),
        new ToolboxItem("Button", "Controls", "WinUI", "<Button Content=\"Button\" />"),
        new ToolboxItem("TextBlock", "Controls", "WinUI", "<TextBlock Text=\"Text\" />"),
        new ToolboxItem("TextBox", "Controls", "WinUI", "<TextBox />"),
        new ToolboxItem("CheckBox", "Controls", "WinUI", "<CheckBox Content=\"Check\" />"),
        new ToolboxItem("RadioButton", "Controls", "WinUI", "<RadioButton Content=\"Radio\" />"),
        new ToolboxItem("ComboBox", "Controls", "WinUI", "<ComboBox />"),
        new ToolboxItem("Slider", "Controls", "WinUI", "<Slider />"),
        new ToolboxItem("ProgressRing", "Controls", "WinUI", "<ProgressRing />"),
        new ToolboxItem("ListView", "Data", "WinUI", "<ListView />"),
        new ToolboxItem("NavigationView", "Navigation", "WinUI", "<NavigationView />"),
        new ToolboxItem("TabView", "Navigation", "WinUI", "<TabView />"),
    };

    public static IReadOnlyList<ToolboxItem> GetHtmlElements() => new[]
    {
        new ToolboxItem("div", "Structure", "HTML", "<div></div>"),
        new ToolboxItem("span", "Structure", "HTML", "<span></span>"),
        new ToolboxItem("section", "Structure", "HTML", "<section></section>"),
        new ToolboxItem("header", "Structure", "HTML", "<header></header>"),
        new ToolboxItem("footer", "Structure", "HTML", "<footer></footer>"),
        new ToolboxItem("button", "Form", "HTML", "<button>Button</button>"),
        new ToolboxItem("input", "Form", "HTML", "<input type=\"text\" />"),
        new ToolboxItem("textarea", "Form", "HTML", "<textarea></textarea>"),
        new ToolboxItem("select", "Form", "HTML", "<select></select>"),
        new ToolboxItem("label", "Form", "HTML", "<label>Label</label>"),
        new ToolboxItem("h1", "Text", "HTML", "<h1>Heading</h1>"),
        new ToolboxItem("p", "Text", "HTML", "<p>Paragraph</p>"),
        new ToolboxItem("a", "Text", "HTML", "<a href=\"#\">Link</a>"),
        new ToolboxItem("img", "Media", "HTML", "<img src=\"\" alt=\"\" />"),
        new ToolboxItem("video", "Media", "HTML", "<video></video>"),
        new ToolboxItem("table", "Data", "HTML", "<table></table>"),
        new ToolboxItem("ul", "List", "HTML", "<ul><li>Item</li></ul>"),
    };

    /// <summary>
    /// Returns the appropriate control list based on the detected target platform.
    /// </summary>
    public static IReadOnlyList<ToolboxItem> GetControlsForPlatform(string platform)
    {
        return platform switch
        {
            "Avalonia" => GetAvaloniaControls(),
            "WPF" => GetWpfControls(),
            "WinUI" or "Uno" => GetWinUiControls(),
            "HTML" => GetHtmlElements(),
            _ => GetAvaloniaControls()
        };
    }
}
