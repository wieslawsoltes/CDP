using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace Avalonia.Diagnostics.Cdp.Domains;

public class ReactiveUiFrameworkProvider : IMvvmFrameworkProvider
{
    public string Name => "ReactiveUI";

    public bool IsViewModel(object dataContext)
    {
        if (dataContext == null) return false;
        var type = dataContext.GetType();
        
        // Exclude standard controls and primitives
        if (type.IsValueType || type == typeof(string)) return false;
        if (dataContext is Avalonia.Visual || dataContext is Avalonia.StyledElement) return false;

        // Check type names or interfaces for ReactiveObject / IReactiveObject
        return ImplementsOrInherits(type, "IReactiveObject") || ImplementsOrInherits(type, "ReactiveObject");
    }

    public IEnumerable<PropertyInfo> GetProperties(object viewModel)
    {
        if (viewModel == null) return Enumerable.Empty<PropertyInfo>();
        return viewModel.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
    }

    public bool IsCommand(object value)
    {
        if (value == null) return false;
        if (value is not System.Windows.Input.ICommand) return false;
        
        var type = value.GetType();
        return ImplementsOrInherits(type, "IReactiveCommand") || ImplementsOrInherits(type, "ReactiveCommand");
    }

    public IDisposable? SubscribeToCommand(object command, Action<object?> onExecuted)
    {
        if (command == null) return null;
        return ObservableSubscriptionHelper.SubscribeDynamic(command, onExecuted);
    }

    private static bool ImplementsOrInherits(Type type, string targetName)
    {
        var current = type;
        while (current != null)
        {
            if (current.Name.Equals(targetName, StringComparison.Ordinal) || 
                current.FullName != null && current.FullName.Contains(targetName, StringComparison.Ordinal))
            {
                return true;
            }
            current = current.BaseType;
        }

        foreach (var iface in type.GetInterfaces())
        {
            if (iface.Name.Equals(targetName, StringComparison.Ordinal) ||
                iface.FullName != null && iface.FullName.Contains(targetName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

public class CommunityToolkitMvvmFrameworkProvider : IMvvmFrameworkProvider
{
    public string Name => "CommunityToolkit.Mvvm";

    public bool IsViewModel(object dataContext)
    {
        if (dataContext == null) return false;
        var type = dataContext.GetType();
        
        if (type.IsValueType || type == typeof(string)) return false;
        if (dataContext is Avalonia.Visual || dataContext is Avalonia.StyledElement) return false;

        return ImplementsOrInherits(type, "ObservableObject") || ImplementsOrInherits(type, "ObservableValidator");
    }

    public IEnumerable<PropertyInfo> GetProperties(object viewModel)
    {
        if (viewModel == null) return Enumerable.Empty<PropertyInfo>();
        return viewModel.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
    }

    public bool IsCommand(object value)
    {
        if (value == null) return false;
        if (value is not System.Windows.Input.ICommand) return false;

        var type = value.GetType();
        return ImplementsOrInherits(type, "IRelayCommand") || ImplementsOrInherits(type, "IAsyncRelayCommand") || 
               type.Name.Contains("RelayCommand", StringComparison.Ordinal);
    }

    public IDisposable? SubscribeToCommand(object command, Action<object?> onExecuted)
    {
        if (command is INotifyPropertyChanged inpc)
        {
            PropertyChangedEventHandler handler = (sender, e) =>
            {
                if (e.PropertyName == "IsRunning")
                {
                    var prop = sender?.GetType().GetProperty("IsRunning");
                    if (prop != null && prop.GetValue(sender) is bool isRunning && isRunning)
                    {
                        onExecuted(null);
                    }
                }
            };
            inpc.PropertyChanged += handler;
            return new ActionDisposable(() => inpc.PropertyChanged -= handler);
        }
        return ObservableSubscriptionHelper.SubscribeDynamic(command, onExecuted);
    }

    private static bool ImplementsOrInherits(Type type, string targetName)
    {
        var current = type;
        while (current != null)
        {
            if (current.Name.Equals(targetName, StringComparison.Ordinal) || 
                current.FullName != null && current.FullName.Contains(targetName, StringComparison.Ordinal))
            {
                return true;
            }
            current = current.BaseType;
        }

        foreach (var iface in type.GetInterfaces())
        {
            if (iface.Name.Equals(targetName, StringComparison.Ordinal) ||
                iface.FullName != null && iface.FullName.Contains(targetName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

public class VanillaMvvmFrameworkProvider : IMvvmFrameworkProvider
{
    public string Name => "Vanilla MVVM";

    public bool IsViewModel(object dataContext)
    {
        if (dataContext == null) return false;
        var type = dataContext.GetType();
        
        if (type.IsValueType || type == typeof(string)) return false;
        if (dataContext is Avalonia.Visual || dataContext is Avalonia.StyledElement) return false;

        // Exclude system, microsoft, avalonia and internal diagnostic namespaces
        string? ns = type.Namespace;
        if (ns != null)
        {
            if (ns.StartsWith("System", StringComparison.Ordinal) ||
                ns.StartsWith("Microsoft", StringComparison.Ordinal) ||
                (ns.StartsWith("Avalonia", StringComparison.Ordinal) && !ns.StartsWith("Avalonia.Diagnostics.Cdp.Tests", StringComparison.Ordinal)) ||
                ns.StartsWith("Chrome.DevTools", StringComparison.Ordinal) ||
                ns.StartsWith("CdpInspectorApp", StringComparison.Ordinal))
            {
                return false;
            }
        }

        // Vanilla ViewModels must implement INotifyPropertyChanged
        return dataContext is INotifyPropertyChanged;
    }

    public IEnumerable<PropertyInfo> GetProperties(object viewModel)
    {
        if (viewModel == null) return Enumerable.Empty<PropertyInfo>();
        return viewModel.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
    }

    public bool IsCommand(object value)
    {
        return value is System.Windows.Input.ICommand;
    }

    public IDisposable? SubscribeToCommand(object command, Action<object?> onExecuted)
    {
        if (command is INotifyPropertyChanged inpc)
        {
            PropertyChangedEventHandler handler = (sender, e) =>
            {
                if (e.PropertyName == "IsRunning")
                {
                    var prop = sender?.GetType().GetProperty("IsRunning");
                    if (prop != null && prop.GetValue(sender) is bool isRunning && isRunning)
                    {
                        onExecuted(null);
                    }
                }
            };
            inpc.PropertyChanged += handler;
            return new ActionDisposable(() => inpc.PropertyChanged -= handler);
        }
        return ObservableSubscriptionHelper.SubscribeDynamic(command, onExecuted);
    }
}

public class ActionDisposable : IDisposable
{
    private readonly Action _action;
    public ActionDisposable(Action action) => _action = action;
    public void Dispose() => _action();
}
