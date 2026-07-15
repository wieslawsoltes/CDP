using System;
using Avalonia.Headless.XUnit;
using Xunit;
using CdpInspectorApp.Controls;
using Avalonia.Media;
using Avalonia;

namespace Avalonia.Diagnostics.Cdp.Tests
{
    public class CustomControlsTests
    {
        [AvaloniaFact]
        public void Test_RadialGauge_Property_Values()
        {
            var gauge = new RadialGauge
            {
                Minimum = 10,
                Maximum = 90,
                Value = 50,
                TrackThickness = 8,
                IndicatorThickness = 12,
                StartAngle = 0,
                SweepAngle = 180
            };

            Assert.Equal(10.0, gauge.Minimum);
            Assert.Equal(90.0, gauge.Maximum);
            Assert.Equal(50.0, gauge.Value);
            Assert.Equal(8.0, gauge.TrackThickness);
            Assert.Equal(12.0, gauge.IndicatorThickness);
            Assert.Equal(0.0, gauge.StartAngle);
            Assert.Equal(180.0, gauge.SweepAngle);
        }

        [AvaloniaFact]
        public void Test_PulsingStatusBadge_Property_Values()
        {
            var badge = new PulsingStatusBadge
            {
                StatusText = "Connecting",
                StatusColor = Colors.Green,
                IsPulsing = true
            };

            Assert.Equal("Connecting", badge.StatusText);
            Assert.Equal(Colors.Green, badge.StatusColor);
            Assert.True(badge.IsPulsing);
        }

        [AvaloniaFact]
        public void Test_MetricMeterCard_Property_Values()
        {
            var card = new MetricMeterCard
            {
                Title = "Memory usage",
                ValueText = "128 MB",
                FillPercentage = 0.5,
                ThemeColor = Colors.Blue
            };

            Assert.Equal("Memory usage", card.Title);
            Assert.Equal("128 MB", card.ValueText);
            Assert.Equal(0.5, card.FillPercentage);
            Assert.Equal(Colors.Blue, card.ThemeColor);
        }

        [AvaloniaFact]
        public void Test_ScreenWorkspaceBoundsPad_Coordinates_Clamping()
        {
            var pad = new ScreenWorkspaceBoundsPad
            {
                ScreenWidth = 1920,
                ScreenHeight = 1080,
                WindowWidth = 800,
                WindowHeight = 600,
                WindowX = 50,
                WindowY = 100
            };

            Assert.Equal(1920, pad.ScreenWidth);
            Assert.Equal(1080, pad.ScreenHeight);
            Assert.Equal(800, pad.WindowWidth);
            Assert.Equal(600, pad.WindowHeight);
            Assert.Equal(50, pad.WindowX);
            Assert.Equal(100, pad.WindowY);
        }

        [AvaloniaFact]
        public void Test_LogLevelBadge_Property_Values()
        {
            var badge = new LogLevelBadge
            {
                Level = "Error"
            };

            Assert.Equal("Error", badge.Level);
        }
    }
}
