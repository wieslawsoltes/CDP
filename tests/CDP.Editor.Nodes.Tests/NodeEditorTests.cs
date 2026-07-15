using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CDP.Editor.Nodes.Views;
using CDP.Editor.Nodes.ViewModels;
using Xunit;

namespace CDP.Editor.Nodes.Tests
{
    public class NodeEditorTests
    {
        [AvaloniaFact]
        public void Test_Node_Editor_View_Initializes()
        {
            var nodeEditorVm = new NodeEditorViewModel();
            var view = new NodeEditorView { DataContext = nodeEditorVm };
            var window = new Window { Content = view };
            window.Show();

            try
            {
                Assert.NotNull(view);
                Assert.Equal(nodeEditorVm, view.DataContext);
            }
            finally
            {
                window.Close();
            }
        }
    }
}
