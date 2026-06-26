using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Models;

public class TestEnvironmentModel : ViewModelBase
{
    private string _name = "";
    private ObservableCollection<EnvironmentVariableModel> _variables = new();
    private string _includedTags = "";
    private string _excludedTags = "";

    public string Name
    {
        get => _name;
        set => RaiseAndSetIfChanged(ref _name, value);
    }

    public ObservableCollection<EnvironmentVariableModel> Variables
    {
        get => _variables;
        set => RaiseAndSetIfChanged(ref _variables, value);
    }

    public string IncludedTags
    {
        get => _includedTags;
        set => RaiseAndSetIfChanged(ref _includedTags, value);
    }

    public string ExcludedTags
    {
        get => _excludedTags;
        set => RaiseAndSetIfChanged(ref _excludedTags, value);
    }
}

public class EnvironmentVariableModel : ViewModelBase
{
    private string _key = "";
    private string _value = "";

    public string Key
    {
        get => _key;
        set => RaiseAndSetIfChanged(ref _key, value);
    }

    public string Value
    {
        get => _value;
        set => RaiseAndSetIfChanged(ref _value, value);
    }
}

[JsonSerializable(typeof(List<TestEnvironmentModel>))]
internal partial class EnvironmentJsonContext : JsonSerializerContext
{
}

