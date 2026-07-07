using System;
using System.Collections.Generic;
using System.Reflection;

namespace Avalonia.Diagnostics.Cdp.Domains;

public interface IMvvmFrameworkProvider
{
    string Name { get; }
    
    // Checks if the DataContext is a valid ViewModel for this framework
    bool IsViewModel(object dataContext);
    
    // Gets properties of the ViewModel
    IEnumerable<PropertyInfo> GetProperties(object viewModel);
    
    // Checks if a property value represents a command in this framework
    bool IsCommand(object value);
    
    // Tries to subscribe to a command to notify execution results.
    // Returns a disposable subscription if successful, or null.
    IDisposable? SubscribeToCommand(object command, Action<object?> onExecuted);
}
