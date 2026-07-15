using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CDP.Editor.Nodes.ViewModels;

namespace CdpGalleryApp.ViewModels;

public class LogicPinViewModel : PinViewModel
{
    private bool _value;
    public bool Value
    {
        get => _value;
        set => RaiseAndSetIfChanged(ref _value, value);
    }
}

public abstract class LogicNodeViewModel : NodeViewModel
{
    public abstract void UpdateLogic();
}

public class SwitchNodeViewModel : LogicNodeViewModel
{
    public SwitchNodeViewModel()
    {
        Name = "Switch";
        Width = 130;
        Height = 75;
        Background = Brush.Parse("#1a237e");
        TitleBackground = Brush.Parse("#283593");

        Outputs.Add(new LogicPinViewModel { Name = "Out", Kind = PinKind.Output, Owner = this, Index = 0 });

        var toggle = new ToggleButton
        {
            Content = "OFF",
            Width = 70,
            Height = 28,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brush.Parse("#2d2d2d"),
            Foreground = Brush.Parse("#e8eaed")
        };
        toggle.Click += (s, e) =>
        {
            bool isChecked = toggle.IsChecked ?? false;
            toggle.Content = isChecked ? "ON" : "OFF";
            toggle.Background = isChecked ? Brush.Parse("#4caf50") : Brush.Parse("#2d2d2d");
        };
        Content = toggle;
    }

    public override void UpdateLogic()
    {
        var isChecked = (Content as ToggleButton)?.IsChecked ?? false;
        if (Outputs.Count > 0 && Outputs[0] is LogicPinViewModel outPin)
        {
            outPin.Value = isChecked;
        }
    }
}

public class ClockNodeViewModel : LogicNodeViewModel
{
    private int _tickCount = 0;

    public ClockNodeViewModel()
    {
        Name = "Clock";
        Width = 130;
        Height = 75;
        Background = Brush.Parse("#4a148c");
        TitleBackground = Brush.Parse("#6a1b9a");

        Outputs.Add(new LogicPinViewModel { Name = "CLK", Kind = PinKind.Output, Owner = this, Index = 0 });

        var tb = new TextBlock
        {
            Text = "CLK: 0",
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush.Parse("#ffd54f")
        };
        Content = tb;
    }

    public override void UpdateLogic()
    {
        _tickCount++;
        if (_tickCount >= 5) // Toggle state every 5 ticks (500ms)
        {
            _tickCount = 0;
            if (Outputs.Count > 0 && Outputs[0] is LogicPinViewModel outPin)
            {
                outPin.Value = !outPin.Value;
                if (Content is TextBlock tb)
                {
                    tb.Text = $"CLK: {(outPin.Value ? "1" : "0")}";
                }
            }
        }
    }
}

public class AndGateNodeViewModel : LogicNodeViewModel
{
    public AndGateNodeViewModel()
    {
        Name = "AND Gate";
        Width = 130;
        Height = 85;
        Background = Brush.Parse("#2e7d32");
        TitleBackground = Brush.Parse("#1b5e20");

        Inputs.Add(new LogicPinViewModel { Name = "A", Kind = PinKind.Input, Owner = this, Index = 0 });
        Inputs.Add(new LogicPinViewModel { Name = "B", Kind = PinKind.Input, Owner = this, Index = 1 });
        Outputs.Add(new LogicPinViewModel { Name = "Out", Kind = PinKind.Output, Owner = this, Index = 0 });

        Content = new TextBlock
        {
            Text = "AND",
            FontWeight = FontWeight.Bold,
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush.Parse("#e8eaed")
        };
    }

    public override void UpdateLogic()
    {
        if (Inputs.Count >= 2 && Outputs.Count >= 1 &&
            Inputs[0] is LogicPinViewModel inA &&
            Inputs[1] is LogicPinViewModel inB &&
            Outputs[0] is LogicPinViewModel outPin)
        {
            outPin.Value = inA.Value && inB.Value;
        }
    }
}

public class OrGateNodeViewModel : LogicNodeViewModel
{
    public OrGateNodeViewModel()
    {
        Name = "OR Gate";
        Width = 130;
        Height = 85;
        Background = Brush.Parse("#c62828");
        TitleBackground = Brush.Parse("#b71c1c");

        Inputs.Add(new LogicPinViewModel { Name = "A", Kind = PinKind.Input, Owner = this, Index = 0 });
        Inputs.Add(new LogicPinViewModel { Name = "B", Kind = PinKind.Input, Owner = this, Index = 1 });
        Outputs.Add(new LogicPinViewModel { Name = "Out", Kind = PinKind.Output, Owner = this, Index = 0 });

        Content = new TextBlock
        {
            Text = "OR",
            FontWeight = FontWeight.Bold,
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush.Parse("#e8eaed")
        };
    }

    public override void UpdateLogic()
    {
        if (Inputs.Count >= 2 && Outputs.Count >= 1 &&
            Inputs[0] is LogicPinViewModel inA &&
            Inputs[1] is LogicPinViewModel inB &&
            Outputs[0] is LogicPinViewModel outPin)
        {
            outPin.Value = inA.Value || inB.Value;
        }
    }
}

public class NotGateNodeViewModel : LogicNodeViewModel
{
    public NotGateNodeViewModel()
    {
        Name = "NOT Gate";
        Width = 130;
        Height = 75;
        Background = Brush.Parse("#ef6c00");
        TitleBackground = Brush.Parse("#e65100");

        Inputs.Add(new LogicPinViewModel { Name = "In", Kind = PinKind.Input, Owner = this, Index = 0 });
        Outputs.Add(new LogicPinViewModel { Name = "Out", Kind = PinKind.Output, Owner = this, Index = 0 });

        Content = new TextBlock
        {
            Text = "NOT",
            FontWeight = FontWeight.Bold,
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush.Parse("#e8eaed")
        };
    }

    public override void UpdateLogic()
    {
        if (Inputs.Count >= 1 && Outputs.Count >= 1 &&
            Inputs[0] is LogicPinViewModel inPin &&
            Outputs[0] is LogicPinViewModel outPin)
        {
            outPin.Value = !inPin.Value;
        }
    }
}

public class DFlipFlopNodeViewModel : LogicNodeViewModel
{
    private bool _prevClk = false;
    private bool _q = false;

    public DFlipFlopNodeViewModel()
    {
        Name = "D Flip-Flop";
        Width = 140;
        Height = 95;
        Background = Brush.Parse("#00838f");
        TitleBackground = Brush.Parse("#006064");

        Inputs.Add(new LogicPinViewModel { Name = "D", Kind = PinKind.Input, Owner = this, Index = 0 });
        Inputs.Add(new LogicPinViewModel { Name = "CLK", Kind = PinKind.Input, Owner = this, Index = 1 });
        Outputs.Add(new LogicPinViewModel { Name = "Q", Kind = PinKind.Output, Owner = this, Index = 0 });
        Outputs.Add(new LogicPinViewModel { Name = "Q'", Kind = PinKind.Output, Owner = this, Index = 1 });

        Content = new TextBlock
        {
            Text = "D-FF\nQ: 0  Q': 1",
            FontWeight = FontWeight.Bold,
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush.Parse("#e8eaed")
        };
    }

    public override void UpdateLogic()
    {
        if (Inputs.Count >= 2 && Outputs.Count >= 2 &&
            Inputs[0] is LogicPinViewModel inD &&
            Inputs[1] is LogicPinViewModel inClk &&
            Outputs[0] is LogicPinViewModel outQ &&
            Outputs[1] is LogicPinViewModel outQBar)
        {
            bool clk = inClk.Value;
            if (!_prevClk && clk) // Rising Edge
            {
                _q = inD.Value;
            }
            _prevClk = clk;

            outQ.Value = _q;
            outQBar.Value = !_q;

            if (Content is TextBlock tb)
            {
                tb.Text = $"D-FF\nQ: {(_q ? "1" : "0")}  Q': {(!_q ? "1" : "0")}";
            }
        }
    }
}

public class JkFlipFlopNodeViewModel : LogicNodeViewModel
{
    private bool _prevClk = false;
    private bool _q = false;

    public JkFlipFlopNodeViewModel()
    {
        Name = "JK Flip-Flop";
        Width = 145;
        Height = 105;
        Background = Brush.Parse("#37474f");
        TitleBackground = Brush.Parse("#263238");

        Inputs.Add(new LogicPinViewModel { Name = "J", Kind = PinKind.Input, Owner = this, Index = 0 });
        Inputs.Add(new LogicPinViewModel { Name = "K", Kind = PinKind.Input, Owner = this, Index = 1 });
        Inputs.Add(new LogicPinViewModel { Name = "CLK", Kind = PinKind.Input, Owner = this, Index = 2 });
        Outputs.Add(new LogicPinViewModel { Name = "Q", Kind = PinKind.Output, Owner = this, Index = 0 });
        Outputs.Add(new LogicPinViewModel { Name = "Q'", Kind = PinKind.Output, Owner = this, Index = 1 });

        Content = new TextBlock
        {
            Text = "JK-FF\nQ: 0  Q': 1",
            FontWeight = FontWeight.Bold,
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush.Parse("#e8eaed")
        };
    }

    public override void UpdateLogic()
    {
        if (Inputs.Count >= 3 && Outputs.Count >= 2 &&
            Inputs[0] is LogicPinViewModel inJ &&
            Inputs[1] is LogicPinViewModel inK &&
            Inputs[2] is LogicPinViewModel inClk &&
            Outputs[0] is LogicPinViewModel outQ &&
            Outputs[1] is LogicPinViewModel outQBar)
        {
            bool clk = inClk.Value;
            if (!_prevClk && clk) // Rising Edge
            {
                bool j = inJ.Value;
                bool k = inK.Value;
                if (j && k) _q = !_q;
                else if (j) _q = true;
                else if (k) _q = false;
            }
            _prevClk = clk;

            outQ.Value = _q;
            outQBar.Value = !_q;

            if (Content is TextBlock tb)
            {
                tb.Text = $"JK-FF\nQ: {(_q ? "1" : "0")}  Q': {(!_q ? "1" : "0")}";
            }
        }
    }
}

public class LedNodeViewModel : LogicNodeViewModel
{
    public LedNodeViewModel()
    {
        Name = "LED Output";
        Width = 120;
        Height = 75;
        Background = Brush.Parse("#4e342e");
        TitleBackground = Brush.Parse("#3e2723");

        Inputs.Add(new LogicPinViewModel { Name = "In", Kind = PinKind.Input, Owner = this, Index = 0 });

        var ledBulb = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Background = Brush.Parse("#3c4043"),
            BorderBrush = Brush.Parse("#5f6368"),
            BorderThickness = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Content = ledBulb;
    }

    public override void UpdateLogic()
    {
        if (Inputs.Count >= 1 && Inputs[0] is LogicPinViewModel inPin && Content is Border led)
        {
            bool val = inPin.Value;
            led.Background = val ? Brush.Parse("#ff1744") : Brush.Parse("#3c4043");
            led.BorderBrush = val ? Brush.Parse("#ff8a80") : Brush.Parse("#5f6368");
        }
    }
}

public class NodeEditorPageViewModel : ViewModelBase
{
    private NodeEditorViewModel _nodeEditorVm;
    private DispatcherTimer _timer;
    private bool _isRunning = true;

    public NodeEditorViewModel NodeEditorVm
    {
        get => _nodeEditorVm;
        set => RaiseAndSetIfChanged(ref _nodeEditorVm, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (RaiseAndSetIfChanged(ref _isRunning, value))
            {
                if (value) _timer.Start();
                else _timer.Stop();
            }
        }
    }

    public ICommand ToggleRunCommand { get; }
    public ICommand StepCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand AddLogicNodeCommand { get; }

    public NodeEditorPageViewModel()
    {
        _nodeEditorVm = new NodeEditorViewModel();

        // Register default creator handler to generate Switch logic nodes when user requests a new node
        _nodeEditorVm.CreateNodeHandler = () => new SwitchNodeViewModel();

        // Spawn default logic circuit (Switch -> AND -> LED)
        var toggle1 = new SwitchNodeViewModel { X = 50, Y = 100 };
        var toggle2 = new SwitchNodeViewModel { X = 50, Y = 220 };
        var andGate = new AndGateNodeViewModel { X = 240, Y = 150 };
        var ledOut = new LedNodeViewModel { X = 430, Y = 155 };

        _nodeEditorVm.Nodes.Add(toggle1);
        _nodeEditorVm.Nodes.Add(toggle2);
        _nodeEditorVm.Nodes.Add(andGate);
        _nodeEditorVm.Nodes.Add(ledOut);

        _nodeEditorVm.Connections.Add(new ConnectionViewModel { FromPin = toggle1.Outputs[0], ToPin = andGate.Inputs[0] });
        _nodeEditorVm.Connections.Add(new ConnectionViewModel { FromPin = toggle2.Outputs[0], ToPin = andGate.Inputs[1] });
        _nodeEditorVm.Connections.Add(new ConnectionViewModel { FromPin = andGate.Outputs[0], ToPin = ledOut.Inputs[0] });

        // Commands
        ToggleRunCommand = new RelayCommand(() => IsRunning = !IsRunning);
        StepCommand = new RelayCommand(RunSimulationStep);
        ClearCommand = new RelayCommand(ClearCircuit);
        AddLogicNodeCommand = new RelayCommand<object>(ExecuteAddLogicNode);

        // Simulation Loop Timer (every 100ms)
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _timer.Tick += (s, e) => RunSimulationStep();
        _timer.Start();
    }

    private void RunSimulationStep()
    {
        // 1. Propagate connection values
        foreach (var conn in NodeEditorVm.Connections)
        {
            if (conn.FromPin is LogicPinViewModel fromPin && conn.ToPin is LogicPinViewModel toPin)
            {
                toPin.Value = fromPin.Value;
            }
        }

        // 2. Execute logic tick of each node
        foreach (var node in NodeEditorVm.Nodes)
        {
            if (node is LogicNodeViewModel logicNode)
            {
                logicNode.UpdateLogic();
            }
        }
    }

    private void ClearCircuit()
    {
        NodeEditorVm.Connections.Clear();
        NodeEditorVm.Nodes.Clear();
    }

    public class AddNodeParameters
    {
        public string Type { get; }
        public double X { get; }
        public double Y { get; }

        public AddNodeParameters(string type, double x, double y)
        {
            Type = type;
            X = x;
            Y = y;
        }
    }

    private void ExecuteAddLogicNode(object? parameter)
    {
        if (parameter == null) return;

        string? type = null;
        double? x = null;
        double? y = null;

        if (parameter is string typeStr)
        {
            type = typeStr;
        }
        else if (parameter is AddNodeParameters p)
        {
            type = p.Type;
            x = p.X;
            y = p.Y;
        }

        if (string.IsNullOrEmpty(type)) return;

        LogicNodeViewModel node = type switch
        {
            "Switch" => new SwitchNodeViewModel(),
            "Clock" => new ClockNodeViewModel(),
            "AND" => new AndGateNodeViewModel(),
            "OR" => new OrGateNodeViewModel(),
            "NOT" => new NotGateNodeViewModel(),
            "DFF" => new DFlipFlopNodeViewModel(),
            "JKFF" => new JkFlipFlopNodeViewModel(),
            "LED" => new LedNodeViewModel(),
            _ => new SwitchNodeViewModel()
        };

        if (x.HasValue && y.HasValue)
        {
            node.X = x.Value;
            node.Y = y.Value;
        }
        else
        {
            // Offset spawn positions sequentially to prevent overlapping
            int count = NodeEditorVm.Nodes.Count;
            node.X = 120 + (count % 4) * 80;
            node.Y = 100 + (count % 4) * 60;
        }

        NodeEditorVm.Nodes.Add(node);
    }

    private class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }

    private class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        public RelayCommand(Action<T?> execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute((T?)parameter);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
