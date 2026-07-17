using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CDP.Editor.Splits.Controls;
using Xunit;

namespace CDP.Editor.Splits.Tests
{
    public class SplitLayoutTests
    {
        [AvaloniaFact]
        public void Test_Split_Layout_Initializes()
        {
            var split = new SuperSplit();
            var window = new Window { Content = split };
            window.Show();

            try
            {
                Assert.NotNull(split);
            }
            finally
            {
                window.Close();
            }
        }
    }
}
