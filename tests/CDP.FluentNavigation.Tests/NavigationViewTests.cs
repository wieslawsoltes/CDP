using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Avalonia.Threading;
using CDP.FluentNavigation;
using Xunit;

namespace CDP.FluentNavigation.Tests
{
    public class NavigationViewTests
    {
        [AvaloniaFact]
        public void Test_Pane_State_Transitions()
        {
            var navView = new NavigationView();
            var window = new Window { Content = navView };
            window.Show();

            try
            {
                var paneBorder = navView.GetVisualDescendants()
                    .OfType<Border>()
                    .FirstOrDefault(b => b.Name == "PART_PaneBorder");
                Assert.NotNull(paneBorder);

                // Expanded Open
                navView.PaneDisplayMode = NavigationViewPaneDisplayMode.Expanded;
                navView.IsPaneOpen = true;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(navView.OpenPaneLength, paneBorder.Width);

                // Expanded Closed
                navView.IsPaneOpen = false;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(navView.CompactPaneLength, paneBorder.Width);

                // Compact Open
                navView.PaneDisplayMode = NavigationViewPaneDisplayMode.Compact;
                navView.IsPaneOpen = true;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(navView.OpenPaneLength, paneBorder.Width);

                // Compact Closed
                navView.IsPaneOpen = false;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(navView.CompactPaneLength, paneBorder.Width);

                // Minimal Open
                navView.PaneDisplayMode = NavigationViewPaneDisplayMode.Minimal;
                navView.IsPaneOpen = true;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(navView.OpenPaneLength, paneBorder.Width);

                // Minimal Closed
                navView.IsPaneOpen = false;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(0.0, paneBorder.Width);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void Test_Item_Selection_Changes()
        {
            var navView = new NavigationView();
            var item1 = new NavigationViewItem { Content = "Item 1" };
            var item2 = new NavigationViewItem { Content = "Item 2" };
            navView.MenuItems.Add(item1);
            navView.MenuItems.Add(item2);

            var window = new Window { Content = navView };
            window.Show();

            try
            {
                bool selectionChangedFired = false;
                navView.SelectionChanged += (s, e) =>
                {
                    selectionChangedFired = true;
                    Assert.Equal(item2, e.SelectedItem);
                };

                navView.SelectedItem = item2;
                Dispatcher.UIThread.RunJobs();

                Assert.True(selectionChangedFired);
                Assert.Equal(item2, navView.SelectedItem);
            }
            finally
            {
                window.Close();
            }
        }
    }
}
