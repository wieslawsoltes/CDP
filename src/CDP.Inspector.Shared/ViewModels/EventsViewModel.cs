using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Input;
using Avalonia.Threading;
using Chrome.DevTools.Protocol;
using CDP.Editor.Splits.Models;
using Avalonia.Layout;

namespace CdpInspectorApp.ViewModels;

public class CdpEventEntry
{
    public string Timestamp { get; set; } = "";
    public string Method { get; set; } = "";
    public string Selector { get; set; } = "";
    public string ParamsJson { get; set; } = "";
}

public class EventsViewModel : ViewModelBase
{
    private readonly ICdpService _cdpService;
    private readonly List<CdpEventEntry> _allEvents = new();
    private ObservableCollection<CdpEventEntry> _filteredEvents = new();
    private CdpEventEntry? _selectedEvent;
    private string _searchQuery = "";
    private bool _ignoreScreencast = true;
    private bool _isPaused = false;
    private string _selectedEventPayload = "";
    private bool _filterDom = true;
    private bool _filterPage = true;
    private bool _filterInput = true;
    private bool _filterRuntime = true;
    private bool _filterConsoleLog = true;
    private bool _filterNetwork = true;
    private bool _filterOther = true;

    private SplitNode? _layoutRoot;
    private BoxNode? _selectedPane;

    public SplitNode? LayoutRoot
    {
        get => _layoutRoot;
        set => RaiseAndSetIfChanged(ref _layoutRoot, value);
    }

    public BoxNode? SelectedPane
    {
        get => _selectedPane;
        set => RaiseAndSetIfChanged(ref _selectedPane, value);
    }

    public void ResetLayout()
    {
        var list = new BoxNode();
        list.AddTab("Logged Events", "TableIcon", "EventsList");

        var payload = new BoxNode();
        payload.AddTab("Event Payload", "EyeIcon", "EventsPayload");

        LayoutRoot = new SplitContainerNode(Orientation.Horizontal, list, payload) { SplitterRatio = 0.4 };
        SelectedPane = list;
    }

    public ObservableCollection<CdpEventEntry> FilteredEvents => _filteredEvents;

    public CdpEventEntry? SelectedEvent
    {
        get => _selectedEvent;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedEvent, value))
            {
                UpdateSelectedEventPayload();
            }
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (RaiseAndSetIfChanged(ref _searchQuery, value))
            {
                ApplyFiltering();
            }
        }
    }

    public bool IgnoreScreencast
    {
        get => _ignoreScreencast;
        set
        {
            if (RaiseAndSetIfChanged(ref _ignoreScreencast, value))
            {
                ApplyFiltering();
            }
        }
    }

    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            RaiseAndSetIfChanged(ref _isPaused, value);
        }
    }

    public bool FilterDom
    {
        get => _filterDom;
        set
        {
            if (RaiseAndSetIfChanged(ref _filterDom, value))
            {
                ApplyFiltering();
            }
        }
    }

    public bool FilterPage
    {
        get => _filterPage;
        set
        {
            if (RaiseAndSetIfChanged(ref _filterPage, value))
            {
                ApplyFiltering();
            }
        }
    }

    public bool FilterInput
    {
        get => _filterInput;
        set
        {
            if (RaiseAndSetIfChanged(ref _filterInput, value))
            {
                ApplyFiltering();
            }
        }
    }

    public bool FilterRuntime
    {
        get => _filterRuntime;
        set
        {
            if (RaiseAndSetIfChanged(ref _filterRuntime, value))
            {
                ApplyFiltering();
            }
        }
    }

    public bool FilterConsoleLog
    {
        get => _filterConsoleLog;
        set
        {
            if (RaiseAndSetIfChanged(ref _filterConsoleLog, value))
            {
                ApplyFiltering();
            }
        }
    }

    public bool FilterNetwork
    {
        get => _filterNetwork;
        set
        {
            if (RaiseAndSetIfChanged(ref _filterNetwork, value))
            {
                ApplyFiltering();
            }
        }
    }

    public bool FilterOther
    {
        get => _filterOther;
        set
        {
            if (RaiseAndSetIfChanged(ref _filterOther, value))
            {
                ApplyFiltering();
            }
        }
    }

    public string SelectedEventPayload
    {
        get => _selectedEventPayload;
        set => RaiseAndSetIfChanged(ref _selectedEventPayload, value);
    }

    public ICommand ClearEventsCommand { get; }

    public EventsViewModel(ICdpService cdpService)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _cdpService.EventReceived += CdpService_EventReceived;
        _cdpService.PropertyChanged += CdpService_PropertyChanged;

        ClearEventsCommand = new RelayCommand(ClearEvents);

        if (_cdpService.IsConnected)
        {
            _ = EnableInputDomainAsync();
        }

        ResetLayout();
    }

    private void CdpService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ICdpService.IsConnected))
        {
            if (_cdpService.IsConnected)
            {
                _ = EnableInputDomainAsync();
            }
        }
    }

    private async Task EnableInputDomainAsync()
    {
        try
        {
            await _cdpService.SendCommandAsync("Input.enable");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error enabling Input domain in Events panel: {ex.Message}");
        }
    }

    private void CdpService_EventReceived(object? sender, CdpEventEventArgs e)
    {
        if (IsPaused) return;

        // Extract parameters
        string method = e.Method;
        JsonObject parameters = e.Params;

        // Skip screencast frames if option is checked
        if (IgnoreScreencast && method == "Page.screencastFrame")
        {
            return;
        }

        string formattedJson = "";
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            formattedJson = JsonSerializer.Serialize(parameters, options);
        }
        catch
        {
            formattedJson = parameters?.ToString() ?? "{}";
        }

        string selector = "";
        try
        {
            if (parameters != null)
            {
                if (parameters.TryGetPropertyValue("selector", out var selNode) && selNode != null)
                {
                    selector = selNode.GetValue<string>() ?? "";
                }
                else if (parameters.TryGetPropertyValue("step", out var stepNode) && stepNode != null)
                {
                    if (stepNode is JsonObject stepObj && stepObj.TryGetPropertyValue("selectors", out var selsNode) && selsNode is JsonArray selsArr && selsArr.Count > 0)
                    {
                        if (selsArr[0] is JsonArray innerArr && innerArr.Count > 0)
                        {
                            selector = innerArr[0]?.GetValue<string>() ?? "";
                        }
                    }
                }
            }
        }
        catch {}

        var entry = new CdpEventEntry
        {
            Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"),
            Method = method,
            Selector = selector,
            ParamsJson = formattedJson
        };

        Dispatcher.UIThread.Post(() =>
        {
            _allEvents.Add(entry);
            if (_allEvents.Count > 500)
            {
                var removed = _allEvents[0];
                _allEvents.RemoveAt(0);
                if (_filteredEvents.Contains(removed))
                {
                    _filteredEvents.Remove(removed);
                }
            }

            if (MatchesFilter(entry))
            {
                _filteredEvents.Add(entry);
            }
        });
    }

    private bool MatchesFilter(CdpEventEntry entry)
    {
        if (IgnoreScreencast && entry.Method == "Page.screencastFrame")
        {
            return false;
        }

        string domain = "";
        int dot = entry.Method.IndexOf('.');
        if (dot >= 0)
        {
            domain = entry.Method.Substring(0, dot).ToUpperInvariant();
        }

        bool passDomain = domain switch
        {
            "DOM" or "DOMDEBUGGER" => FilterDom,
            "PAGE" => FilterPage,
            "INPUT" => FilterInput,
            "RUNTIME" => FilterRuntime,
            "CONSOLE" or "LOG" => FilterConsoleLog,
            "NETWORK" => FilterNetwork,
            _ => FilterOther
        };

        if (!passDomain)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            return true;
        }

        string q = SearchQuery.ToLowerInvariant();
        return entry.Method.ToLowerInvariant().Contains(q) || entry.ParamsJson.ToLowerInvariant().Contains(q);
    }

    private void ApplyFiltering()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _filteredEvents.Clear();
            foreach (var entry in _allEvents)
            {
                if (MatchesFilter(entry))
                {
                    _filteredEvents.Add(entry);
                }
            }
            UpdateSelectedEventPayload();
        });
    }

    private void UpdateSelectedEventPayload()
    {
        if (SelectedEvent != null)
        {
            SelectedEventPayload = SelectedEvent.ParamsJson;
        }
        else
        {
            SelectedEventPayload = "";
        }
    }

    private void ClearEvents()
    {
        _allEvents.Clear();
        _filteredEvents.Clear();
        SelectedEvent = null;
        SelectedEventPayload = "";
    }
}
