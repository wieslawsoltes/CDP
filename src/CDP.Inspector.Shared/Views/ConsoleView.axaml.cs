using Avalonia.Controls;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Views;

[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DataGrid is not trim-safe")]
public partial class ConsoleView : UserControl
{
    public Button BtnClearLogs => btnClearLogs;
    public DataGrid ListLogs => listLogs;
    public ListBox ListConsole => listConsole;
    public TextBox TxtConsoleInput => txtConsoleInput;
    public Button BtnSendConsole => btnSendConsole;
    public TextBox TxtPinnedExpression => txtPinnedExpression;

    public ConsoleView()
    {
        InitializeComponent();
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
}
