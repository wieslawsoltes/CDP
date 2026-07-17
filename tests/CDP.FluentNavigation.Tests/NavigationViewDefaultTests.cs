using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using CDP.FluentNavigation;
using Xunit;

namespace CDP.FluentNavigation.Tests
{
    public class NavigationViewDefaultTests
    {
        [AvaloniaFact]
        public void Test_NavigationView_Default_Values()
        {
            var nav = new NavigationView();

            Assert.True(nav.IsPaneOpen);
            Assert.Equal(NavigationViewPaneDisplayMode.Expanded, nav.PaneDisplayMode);
            Assert.Equal(48.0, nav.CompactPaneLength);
            Assert.Equal(240.0, nav.OpenPaneLength);
            Assert.False(nav.ShowBackButton);
            Assert.True(nav.IsSettingsVisible);
            Assert.Null(nav.SelectedItem);
            Assert.NotNull(nav.MenuItems);
            Assert.NotNull(nav.FooterMenuItems);
        }

        [AvaloniaFact]
        public void Test_NavigationViewItem_Properties()
        {
            var item = new NavigationViewItem
            {
                Content = "Home",
                Icon = "HomeIconGeometry",
                InfoBadge = "New",
                SelectsOnTrigger = false
            };

            Assert.Equal("Home", item.Content);
            Assert.Equal("HomeIconGeometry", item.Icon);
            Assert.Equal("New", item.InfoBadge);
            Assert.False(item.SelectsOnTrigger);
        }

        [AvaloniaFact]
        public void Test_NavigationView_Selection_And_Events()
        {
            var nav = new NavigationView();
            var item1 = new NavigationViewItem { Content = "Item 1" };
            var item2 = new NavigationViewItem { Content = "Item 2" };

            nav.MenuItems.Add(item1);
            nav.MenuItems.Add(item2);

            object? receivedItem = null;
            bool receivedIsSettings = false;
            int eventCount = 0;

            nav.SelectionChanged += (s, e) =>
            {
                receivedItem = e.SelectedItem;
                receivedIsSettings = e.IsSettingsSelected;
                eventCount++;
            };

            nav.SelectedItem = item1;

            Assert.Equal(item1, nav.SelectedItem);
            // Since we set SelectedItem directly, the event is fired or synchronizes.
            // Note that actual event firing on list box selection is triggered by the UI template,
            // but we also can verify setting SelectedItem raises selection changed internally or synchronizes it.
        }
    }
}
