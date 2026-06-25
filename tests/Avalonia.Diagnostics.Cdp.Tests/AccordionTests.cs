using Avalonia.Headless.XUnit;
using Xunit;
using CdpInspectorApp.Controls;
using Avalonia.Controls;
using System;

namespace Avalonia.Diagnostics.Cdp.Tests
{
    public class AccordionTests
    {
        [AvaloniaFact]
        public void Test_Accordion_Exclusivity()
        {
            var app = Avalonia.Application.Current;
            Assert.NotNull(app);

            var sharedStyles = new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://Avalonia.Diagnostics.Cdp.Tests/"))
            {
                Source = new Uri("avares://CDP.Inspector.Shared/Styles.axaml")
            };
            app.Styles.Add(sharedStyles);

            try
            {
                var accordion = new Accordion { IsExclusive = true };
                var item1 = new AccordionItem { Header = "Item 1", IsExpanded = true };
                var item2 = new AccordionItem { Header = "Item 2", IsExpanded = false };
                var item3 = new AccordionItem { Header = "Item 3", IsExpanded = false };

                accordion.Items.Add(item1);
                accordion.Items.Add(item2);
                accordion.Items.Add(item3);

                var window = new Window { Content = accordion };
                window.Show();

                try
                {
                    Assert.Same(accordion, item1.ParentAccordion);
                    Assert.Same(accordion, item2.ParentAccordion);
                    Assert.Same(accordion, item3.ParentAccordion);

                    Assert.True(item1.IsExpanded);
                    Assert.False(item2.IsExpanded);
                    Assert.False(item3.IsExpanded);

                    // Expand item 2, item 1 should be collapsed
                    item2.IsExpanded = true;
                    Assert.False(item1.IsExpanded);
                    Assert.True(item2.IsExpanded);
                    Assert.False(item3.IsExpanded);

                    // Expand item 3, item 2 should be collapsed
                    item3.IsExpanded = true;
                    Assert.False(item1.IsExpanded);
                    Assert.False(item2.IsExpanded);
                    Assert.True(item3.IsExpanded);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                app.Styles.Remove(sharedStyles);
            }
        }

        [AvaloniaFact]
        public void Test_Accordion_Initialization_Multiple_Expanded()
        {
            var app = Avalonia.Application.Current;
            Assert.NotNull(app);

            var sharedStyles = new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://Avalonia.Diagnostics.Cdp.Tests/"))
            {
                Source = new Uri("avares://CDP.Inspector.Shared/Styles.axaml")
            };
            app.Styles.Add(sharedStyles);

            try
            {
                var accordion = new Accordion { IsExclusive = true };
                var item1 = new AccordionItem { Header = "Item 1", IsExpanded = true };
                var item2 = new AccordionItem { Header = "Item 2", IsExpanded = true };

                accordion.Items.Add(item1);
                accordion.Items.Add(item2);

                var window = new Window { Content = accordion };
                window.Show();

                try
                {
                    // Sequential attachment of logical children attaches item 1 first.
                    // When item 1 attaches, it triggers OnItemExpanded, which collapses item 2 before item 2 attaches.
                    // As a result, item 1 remains expanded, and item 2 is collapsed, maintaining exclusivity.
                    Assert.True(item1.IsExpanded);
                    Assert.False(item2.IsExpanded);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                app.Styles.Remove(sharedStyles);
            }
        }
    }
}
