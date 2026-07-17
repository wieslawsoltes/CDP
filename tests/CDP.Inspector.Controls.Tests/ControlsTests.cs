using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CdpInspectorApp.Controls;
using Xunit;

namespace CDP.Inspector.Controls.Tests
{
    public class ControlsTests
    {
        [AvaloniaFact]
        public void Test_Accordion_Initializes()
        {
            var accordion = new Accordion();
            var item = new AccordionItem { Header = "Test Header", Content = "Test Content" };
            accordion.Items.Add(item);

            var window = new Window { Content = accordion };
            window.Show();

            try
            {
                Assert.NotNull(accordion);
                Assert.Single(accordion.Items);
                Assert.False(item.IsExpanded);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void Test_CpuPieChart_Initializes()
        {
            var chart = new CpuPieChart();
            var window = new Window { Content = chart };
            window.Show();

            try
            {
                Assert.NotNull(chart);
            }
            finally
            {
                window.Close();
            }
        }
    }
}
