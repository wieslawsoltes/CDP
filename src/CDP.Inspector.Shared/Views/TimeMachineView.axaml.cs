using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using CdpInspectorApp.ViewModels;
using CDP.Editor.Splits.Controls;
using Chrome.DevTools.Protocol;

namespace CdpInspectorApp.Views;

public partial class TimeMachineView : UserControl
{
    private readonly System.Collections.Generic.Dictionary<string, Control> _viewsCache = new();
    private Point _dragStartPoint;
    private ListBoxItem? _pressedItem;
    private PointerPressedEventArgs? _pressedEventArgs;

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

    public TimeMachineView()
    {
        InitializeComponent();

        HiddenPanel.Children.Clear();

        _viewsCache["TimeMachineFrames"] = TimeMachineFramesPanel;
        _viewsCache["TimeMachineDetails"] = TimeMachineDetailsPanel;

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

        var framesListBox = this.FindControl<ListBox>("FramesListBox");
        if (framesListBox != null)
        {
            framesListBox.AddHandler(InputElement.PointerPressedEvent, FramesListBox_PointerPressed, RoutingStrategies.Tunnel);
            framesListBox.AddHandler(InputElement.PointerMovedEvent, FramesListBox_PointerMoved, RoutingStrategies.Tunnel);
            framesListBox.AddHandler(InputElement.PointerReleasedEvent, FramesListBox_PointerReleased, RoutingStrategies.Tunnel);
        }
    }

    public void FramesListBox_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is ListBox listBox)
        {
            var visualSource = e.Source as Avalonia.Visual;
            ListBoxItem? listBoxItem = null;
            while (visualSource != null)
            {
                if (visualSource is ListBoxItem item)
                {
                    listBoxItem = item;
                    break;
                }
                visualSource = visualSource.GetVisualParent();
            }

            if (listBoxItem != null && listBoxItem.DataContext is TimeMachineFrame)
            {
                _pressedItem = listBoxItem;
                _pressedEventArgs = e;
                _dragStartPoint = e.GetPosition(this);
            }
        }
    }

    public async void FramesListBox_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedItem != null && _pressedEventArgs != null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var currentPos = e.GetPosition(this);
            var diff = currentPos - _dragStartPoint;
            if (Math.Abs(diff.X) > 4 || Math.Abs(diff.Y) > 4)
            {
                var listBoxItem = _pressedItem;
                var pressedArgs = _pressedEventArgs;
                _pressedItem = null;
                _pressedEventArgs = null;

                if (listBoxItem.DataContext is TimeMachineFrame frame)
                {
                    var dataObject = new DataTransfer();
                    var item = new DataTransferItem();
                    item.Set(ScratchView.TimeMachineFrameFormat, frame);
                    item.Set(DataFormat.Text, frame.Index.ToString());
                    dataObject.Add(item);
                    await DragDrop.DoDragDropAsync(pressedArgs, dataObject, DragDropEffects.Copy);
                }
            }
        }
    }

    public void FramesListBox_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _pressedItem = null;
        _pressedEventArgs = null;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainWindowViewModel vm)
        {
            vm.TimeMachine.FileSavePickerHandler = async (title, defaultName) =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel != null)
                {
                    var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = title,
                        DefaultExtension = "tm",
                        SuggestedStartLocation = null,
                        FileTypeChoices = new[]
                        {
                            new FilePickerFileType("Time Machine Sessions (*.tm)")
                            {
                                Patterns = new[] { "*.tm" }
                            }
                        }
                    });
                    return file?.Path.LocalPath;
                }
                return null;
            };

            vm.TimeMachine.FileLoadPickerHandler = async (title) =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel != null)
                {
                    var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = title,
                        AllowMultiple = false,
                        FileTypeFilter = new[]
                        {
                            new FilePickerFileType("Time Machine Sessions (*.tm)")
                            {
                                Patterns = new[] { "*.tm" }
                            }
                        }
                    });
                    if (files != null && files.Count > 0)
                    {
                        return files[0].Path.LocalPath;
                    }
                }
                return null;
            };
        }
    }
}
