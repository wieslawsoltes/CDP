using Avalonia.Controls;
using CdpInspectorApp.ViewModels;
using CDP.Editor.Splits.Controls;

namespace CdpInspectorApp.Views;

[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DataGrid is not trim-safe")]
public partial class ConsoleView : UserControl
{
    private readonly System.Collections.Generic.Dictionary<string, Control> _viewsCache = new();

    public Button BtnClearLogs => btnClearLogs;
    public DataGrid ListLogs => listLogs;
    public ListBox ListConsole => listConsole;
    public TextBox TxtConsoleInput => txtConsoleInput;
    public Button BtnSendConsole => btnSendConsole;
    public TextBox TxtPinnedExpression => txtPinnedExpression;

    private void DetachControl(Control control)
    {
        if (control.Parent is Panel panel)
        {
            panel.Children.Remove(control);
        }
        else if (control.Parent is ContentControl contentControl)
        {
            contentControl.Content = null;
        }
        else if (control.Parent is SuperSplitBox splitBox)
        {
            splitBox.InnerContent = null;
        }
    }

    public ConsoleView()
    {
        InitializeComponent();

        var logsPanel = ConsoleLogsPanel;
        var watchPanel = ConsoleWatchPanel;

        HiddenPanel.Children.Clear();

        _viewsCache["ConsoleLogs"] = logsPanel;
        _viewsCache["ConsoleWatch"] = watchPanel;

        SplitControl.ViewResolver = (viewName, targetBox) =>
        {
            if (_viewsCache.TryGetValue(viewName, out var cached))
            {
                if (targetBox == null || cached.Parent != targetBox)
                {
                    DetachControl(cached);
                }
                return cached;
            }
            return new Control();
        };

        txtConsoleInput.KeyDown += (sender, e) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                if (vm.Console.IsCompletionActive)
                {
                    if (e.Key == Avalonia.Input.Key.Down)
                    {
                        if (vm.Console.Completions.Count > 0)
                        {
                            vm.Console.SelectedCompletionIndex = (vm.Console.SelectedCompletionIndex + 1) % vm.Console.Completions.Count;
                        }
                        e.Handled = true;
                    }
                    else if (e.Key == Avalonia.Input.Key.Up)
                    {
                        if (vm.Console.Completions.Count > 0)
                        {
                            vm.Console.SelectedCompletionIndex = (vm.Console.SelectedCompletionIndex - 1 + vm.Console.Completions.Count) % vm.Console.Completions.Count;
                        }
                        e.Handled = true;
                    }
                    else if (e.Key == Avalonia.Input.Key.Tab || e.Key == Avalonia.Input.Key.Enter)
                    {
                        var selected = vm.Console.SelectedCompletionIndex;
                        if (selected >= 0 && selected < vm.Console.Completions.Count)
                        {
                            var text = txtConsoleInput.Text ?? "";
                            var caret = txtConsoleInput.CaretIndex;
                            var completion = vm.Console.Completions[selected];

                            if (vm.Console.IsUiReplMode)
                            {
                                int spaceIndex = text.LastIndexOf(' ', Math.Max(0, caret - 1));
                                string prefix = spaceIndex >= 0 ? text.Substring(0, spaceIndex + 1) : "";
                                txtConsoleInput.Text = prefix + completion;
                            }
                            else
                            {
                                int dotIndex = text.LastIndexOf('.', Math.Max(0, caret - 1));
                                string prefix = dotIndex >= 0 ? text.Substring(0, dotIndex + 1) : "";
                                txtConsoleInput.Text = prefix + completion;
                            }
                            txtConsoleInput.CaretIndex = txtConsoleInput.Text.Length;
                            vm.Console.IsCompletionActive = false;
                            e.Handled = true;
                        }
                    }
                    else if (e.Key == Avalonia.Input.Key.Escape)
                    {
                        vm.Console.IsCompletionActive = false;
                        e.Handled = true;
                    }
                }
                else
                {
                    if (e.Key == Avalonia.Input.Key.Up)
                    {
                        var prev = vm.Console.GetPreviousHistoryLine();
                        if (prev != null)
                        {
                            txtConsoleInput.Text = prev;
                            txtConsoleInput.CaretIndex = prev.Length;
                            e.Handled = true;
                        }
                    }
                    else if (e.Key == Avalonia.Input.Key.Down)
                    {
                        var next = vm.Console.GetNextHistoryLine();
                        txtConsoleInput.Text = next ?? "";
                        txtConsoleInput.CaretIndex = txtConsoleInput.Text.Length;
                        e.Handled = true;
                    }
                    else if (e.Key == Avalonia.Input.Key.Enter)
                    {
                        vm.Console.EvaluateCommand.Execute(null);
                        e.Handled = true;
                    }
                }
            }
        };

        txtConsoleInput.PropertyChanged += (sender, e) =>
        {
            if (e.Property.Name == nameof(TextBox.CaretIndex))
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    var text = txtConsoleInput.Text ?? "";
                    var caret = txtConsoleInput.CaretIndex;
                    _ = vm.Console.QueryCompletionsAsync(text, caret);
                }
            }
        };

        txtPinnedExpression.KeyDown += (sender, e) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                if (e.Key == Avalonia.Input.Key.Enter)
                {
                    if (vm.Console.AddPinnedExpressionCommand.CanExecute(null))
                    {
                        vm.Console.AddPinnedExpressionCommand.Execute(null);
                        e.Handled = true;
                    }
                }
            }
        };
    }

    private void OnLogEntryDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is DataGrid dg && dg.SelectedItem is CdpInspectorApp.Models.LogModel log)
        {
            if (string.IsNullOrEmpty(log.Text)) return;

            var match = System.Text.RegularExpressions.Regex.Match(log.Text, @"\bin\s+([^:\n\r]+):line\s*(\d+)");
            if (match.Success)
            {
                string path = match.Groups[1].Value.Trim();
                if (int.TryParse(match.Groups[2].Value, out int line))
                {
                    if (DataContext is MainWindowViewModel vm)
                    {
                        vm.NavigateToSource(path, line);
                    }
                }
            }
        }
    }
}
