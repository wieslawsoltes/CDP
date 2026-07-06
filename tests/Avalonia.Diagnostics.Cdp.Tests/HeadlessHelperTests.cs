using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CDP.Automation.Headless;
using Xunit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class HeadlessHelperTests
{
    [AvaloniaFact]
    public async Task TestClickControlHelper()
    {
        var clicked = false;
        var button = new Button { Width = 100, Height = 50 };
        button.Click += (s, e) => clicked = true;

        var window = new Window { Width = 200, Height = 200, Content = button };
        window.Show();

        await Task.Delay(100);
        Dispatcher.UIThread.RunJobs();

        window.ClickControl(button, MouseButton.Left, RawInputModifiers.None);

        Assert.True(clicked);
        window.Close();
    }

    [AvaloniaFact]
    public async Task TestInputTextAndClearControlHelpers()
    {
        var textBox = new TextBox { Width = 150, Height = 40 };
        var window = new Window { Width = 200, Height = 200, Content = textBox };
        window.Show();

        await Task.Delay(100);
        Dispatcher.UIThread.RunJobs();

        // Test InputText
        window.InputText(textBox, "Hello Cdp Headless!");
        Assert.Equal("Hello Cdp Headless!", textBox.Text);

        // Test ClearControl
        window.ClearControl(textBox);
        Assert.Equal("", textBox.Text);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestLongPressControlAsyncHelper()
    {
        var buttonPressed = false;
        var buttonReleased = false;
        var button = new Button { Width = 100, Height = 50 };
        
        button.AddHandler(InputElement.PointerPressedEvent, (s, e) => buttonPressed = true, handledEventsToo: true);
        button.AddHandler(InputElement.PointerReleasedEvent, (s, e) => buttonReleased = true, handledEventsToo: true);

        var window = new Window { Width = 200, Height = 200, Content = button };
        window.Show();

        await Task.Delay(100);
        Dispatcher.UIThread.RunJobs();

        await window.LongPressControlAsync(button, 100);

        Assert.True(buttonPressed);
        Assert.True(buttonReleased);
        window.Close();
    }

    [AvaloniaFact]
    public async Task TestDragAndDropHelper()
    {
        var source = new Button { Name = "source", Width = 50, Height = 50 };
        var target = new Button { Name = "target", Width = 50, Height = 50 };
        var panel = new StackPanel { Children = { source, target } };

        var window = new Window { Width = 400, Height = 400, Content = panel };

        Point? finalPointerPosition = null;
        window.AddHandler(InputElement.PointerMovedEvent, (s, e) =>
        {
            finalPointerPosition = e.GetPosition(window);
        }, handledEventsToo: true);

        window.Show();

        await Task.Delay(100);
        Dispatcher.UIThread.RunJobs();

        var endPoint = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window) ?? new Point();

        window.DragAndDrop(source, target);

        Assert.NotNull(finalPointerPosition);
        Assert.Equal(endPoint.X, finalPointerPosition.Value.X, 1);
        Assert.Equal(endPoint.Y, finalPointerPosition.Value.Y, 1);
        window.Close();
    }
}
