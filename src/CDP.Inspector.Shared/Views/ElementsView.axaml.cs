using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Controls.Presenters;

namespace CdpInspectorApp.Views;

[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DataGrid is not trim-safe")]
public partial class ElementsView : UserControl
{
    private readonly System.Collections.Generic.Dictionary<string, Control> _viewsCache = new();

    private Control GetOrCreateViewInstance(string viewName, CDP.Editor.Splits.Controls.SuperSplitBox? targetBox = null)
    {
        string cacheKey = viewName;
        if (viewName == "DomTree") cacheKey = "pnlDomTree";
        else if (viewName == "AccessibilityTree") cacheKey = "pnlAccessibilityTree";
        else if (viewName == "Styles") cacheKey = "pnlStyles";
        else if (viewName == "Attributes") cacheKey = "pnlAttributes";
        else if (viewName == "Properties") cacheKey = "pnlProperties";
        else if (viewName == "EventListeners") cacheKey = "pnlEventListeners";
        else if (viewName == "Accessibility") cacheKey = "pnlAccessibility";
        else if (viewName == "Layout") cacheKey = "pnlLayout";

        if (_viewsCache.TryGetValue(cacheKey, out var cached))
        {
            if (targetBox == null || cached.Parent != targetBox)
            {
                DetachControl(cached);
            }
            return cached;
        }
        return new TextBlock { Text = $"View {viewName} not found", Margin = new Thickness(10) };
    }

    private void DetachControl(Control control)
    {
        if (control.Parent is CDP.Editor.Splits.Controls.SuperSplitBox splitBox)
        {
            splitBox.InnerContent = null;
            splitBox.UpdateLayout();
        }
        else if (control.Parent is Panel panel)
        {
            panel.Children.Remove(control);
        }
        else if (control.Parent is ContentControl contentControl)
        {
            contentControl.Content = null;
        }

        var visualParent = control.GetVisualParent();
        if (visualParent is ContentPresenter presenter)
        {
            presenter.Content = null;
        }
        else if (visualParent is Panel visualPanel)
        {
            visualPanel.Children.Remove(control);
        }
    }
    public TextBox TxtSearch => txtSearch;
    public Button BtnSearch => btnSearch;
    public DataGrid TreeDom => treeDom;
    public TextBlock TxtSelectedNodeId => txtSelectedNodeId;
    public Button BtnFocus => btnFocus;
    public CheckBox ChkHighlight => chkHighlight;
    public Button BtnDeleteControl => btnDeleteControl;

    public DataGrid ListCssProperties => listCssProperties;
    public TextBox TxtStyleText => txtStyleText;
    public Button BtnApplyStyleText => btnApplyStyleText;

    public DataGrid ListComputedStyles => listComputedStyles;

    public DataGrid ListAttributes => listAttributes;
    public TextBox TxtAttrName => txtAttrName;
    public TextBox TxtAttrValue => txtAttrValue;
    public Button BtnApplyAttr => btnApplyAttr;
    public Button BtnDeleteAttr => btnDeleteAttr;

    public DataGrid ListProperties => listProperties;
    public TextBlock LblSelectedProperty => lblSelectedProperty;
    public TextBox TxtPropertyValue => txtPropertyValue;
    public Button BtnApplyProperty => btnApplyProperty;

    public DataGrid ListEventListeners => listEventListeners;

    public ElementsView()
    {
        InitializeComponent();

        // Initialize view cache
        var hiddenPanel = this.FindControl<Grid>("HiddenPanel");
        if (hiddenPanel != null)
        {
            var children = System.Linq.Enumerable.ToList(hiddenPanel.Children);
            foreach (var child in children)
            {
                if (child is Control ctrl && !string.IsNullOrEmpty(ctrl.Name))
                {
                    hiddenPanel.Children.Remove(ctrl);
                    _viewsCache[ctrl.Name] = ctrl;
                }
            }
        }

        SplitControl.ViewResolver = (viewName, targetBox) => GetOrCreateViewInstance(viewName, targetBox);

        treeDom.SelectionChanged += (s, e) => ScrollSelectedIntoView();

        listCssProperties.CellEditEnded += (s, e) =>
        {
            if (e.EditAction == DataGridEditAction.Commit && e.Row.DataContext is CdpInspectorApp.Models.CssPropertyModel cssProp)
            {
                if (DataContext is CdpInspectorApp.ViewModels.MainWindowViewModel mvm)
                {
                    _ = mvm.Elements.UpdateCssPropertyAsync(cssProp);
                }
                else if (DataContext is CdpInspectorApp.ViewModels.ElementsViewModel evm)
                {
                    _ = evm.UpdateCssPropertyAsync(cssProp);
                }
            }
        };

        listAttributes.CellEditEnded += (s, e) =>
        {
            if (e.EditAction == DataGridEditAction.Commit && e.Row.DataContext is CdpInspectorApp.Models.AttributeModel attr)
            {
                if (DataContext is CdpInspectorApp.ViewModels.MainWindowViewModel mvm)
                {
                    _ = mvm.Elements.UpdateAttributeAsync(attr);
                }
                else if (DataContext is CdpInspectorApp.ViewModels.ElementsViewModel evm)
                {
                    _ = evm.UpdateAttributeAsync(attr);
                }
            }
        };

        listProperties.CellEditEnded += (s, e) =>
        {
            if (e.EditAction == DataGridEditAction.Commit && e.Row.DataContext is CdpInspectorApp.Models.PropertyModel prop)
            {
                if (DataContext is CdpInspectorApp.ViewModels.MainWindowViewModel mvm)
                {
                    _ = mvm.Elements.UpdatePropertyAsync(prop);
                }
                else if (DataContext is CdpInspectorApp.ViewModels.ElementsViewModel evm)
                {
                    _ = evm.UpdatePropertyAsync(prop);
                }
            }
        };
    }

    private void OnBoxValueDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is Control control && control.Tag is string field)
        {
            CdpInspectorApp.ViewModels.ElementsViewModel? evm = null;
            if (DataContext is CdpInspectorApp.ViewModels.MainWindowViewModel mvm)
            {
                evm = mvm.Elements;
            }
            else if (DataContext is CdpInspectorApp.ViewModels.ElementsViewModel directEvm)
            {
                evm = directEvm;
            }

            if (evm != null)
            {
                evm.StartEdit(field);
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var textBox = this.Find<TextBox>("txt" + field);
                    if (textBox != null)
                    {
                        textBox.Focus();
                        textBox.SelectAll();
                    }
                }, Avalonia.Threading.DispatcherPriority.Input);
            }
        }
    }

    private void OnBoxTextBoxKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        CdpInspectorApp.ViewModels.ElementsViewModel? evm = null;
        if (DataContext is CdpInspectorApp.ViewModels.MainWindowViewModel mvm)
        {
            evm = mvm.Elements;
        }
        else if (DataContext is CdpInspectorApp.ViewModels.ElementsViewModel directEvm)
        {
            evm = directEvm;
        }

        if (evm != null)
        {
            if (e.Key == Avalonia.Input.Key.Enter && sender is TextBox textBox && textBox.Tag is string field)
            {
                _ = evm.CommitEditAsync(field);
            }
            else if (e.Key == Avalonia.Input.Key.Escape)
            {
                evm.CancelAllEdits();
            }
        }
    }

    private void OnBoxTextBoxLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CdpInspectorApp.ViewModels.ElementsViewModel? evm = null;
        if (DataContext is CdpInspectorApp.ViewModels.MainWindowViewModel mvm)
        {
            evm = mvm.Elements;
        }
        else if (DataContext is CdpInspectorApp.ViewModels.ElementsViewModel directEvm)
        {
            evm = directEvm;
        }

        if (evm != null && sender is TextBox textBox && textBox.Tag is string field)
        {
            _ = evm.CommitEditAsync(field);
        }
    }

    private void OnAddClassTextBoxKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter)
        {
            CdpInspectorApp.ViewModels.ElementsViewModel? evm = null;
            if (DataContext is CdpInspectorApp.ViewModels.MainWindowViewModel mvm)
            {
                evm = mvm.Elements;
            }
            else if (DataContext is CdpInspectorApp.ViewModels.ElementsViewModel directEvm)
            {
                evm = directEvm;
            }

            if (evm != null && evm.AddClassCommand.CanExecute(null))
            {
                evm.AddClassCommand.Execute(null);
            }
        }
    }

    private void ScrollSelectedIntoView()
    {
        var selected = treeDom.SelectedItem;
        if (selected == null) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            treeDom.ScrollIntoView(selected, null);
        }, Avalonia.Threading.DispatcherPriority.Background);
    }
}
