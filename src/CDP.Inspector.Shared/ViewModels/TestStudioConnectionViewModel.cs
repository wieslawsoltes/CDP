#nullable enable

namespace CdpInspectorApp.ViewModels;

public class TestStudioConnectionViewModel : CDP.Editor.Nodes.ViewModels.ConnectionViewModel
{
    public new TestStudioNodeViewModel? FromNode
    {
        get => base.FromNode as TestStudioNodeViewModel;
        set => base.FromNode = value;
    }

    public new TestStudioNodeViewModel? ToNode
    {
        get => base.ToNode as TestStudioNodeViewModel;
        set => base.ToNode = value;
    }

    public TestStudioConnectionViewModel()
    {
    }

    public TestStudioConnectionViewModel(TestStudioNodeViewModel fromNode, TestStudioNodeViewModel toNode)
        : base(fromNode, toNode)
    {
    }
}
