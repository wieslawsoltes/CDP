using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;

namespace CdpInspectorApp.ViewModels;

public class NetworkViewModel : ViewModelBase
{
    private readonly ICdpService _cdpService;
    private ObservableCollection<NetworkRequestModel> _networkRequests = new();
    private NetworkRequestModel? _selectedRequest;
    private string _selectedUrl = "Select a request";
    private string _selectedRequestHeaders = "";
    private string _selectedResponseHeaders = "";
    private string _selectedResponseBody = "";

    private ObservableCollection<ThrottlingProfile> _throttlingProfiles = new()
    {
        new ThrottlingProfile("No Throttling", false, 0, -1, -1),
        new ThrottlingProfile("Fast 3G", false, 100, 200000, 96000),
        new ThrottlingProfile("Slow 3G", false, 400, 47000, 47000),
        new ThrottlingProfile("Offline", true, 0, 0, 0)
    };
    private ThrottlingProfile? _selectedProfile;

    public ObservableCollection<NetworkRequestModel> NetworkRequests => _networkRequests;
    public ObservableCollection<ThrottlingProfile> ThrottlingProfiles => _throttlingProfiles;

    public ThrottlingProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedProfile, value))
            {
                _ = ApplyThrottlingAsync();
            }
        }
    }

    public NetworkRequestModel? SelectedRequest
    {
        get => _selectedRequest;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedRequest, value))
            {
                UpdateSelectedRequestDetails();
            }
        }
    }

    public string SelectedUrl
    {
        get => _selectedUrl;
        private set => RaiseAndSetIfChanged(ref _selectedUrl, value);
    }

    public string SelectedRequestHeaders
    {
        get => _selectedRequestHeaders;
        private set => RaiseAndSetIfChanged(ref _selectedRequestHeaders, value);
    }

    public string SelectedResponseHeaders
    {
        get => _selectedResponseHeaders;
        private set => RaiseAndSetIfChanged(ref _selectedResponseHeaders, value);
    }

    public string SelectedResponseBody
    {
        get => _selectedResponseBody;
        private set => RaiseAndSetIfChanged(ref _selectedResponseBody, value);
    }

    public ICommand ClearNetworkCommand { get; }

    public NetworkViewModel(ICdpService cdpService)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _cdpService.PropertyChanged += CdpService_PropertyChanged;
        _cdpService.EventReceived += CdpService_EventReceived;

        _selectedProfile = _throttlingProfiles[0];
        ClearNetworkCommand = new RelayCommand(ClearNetwork);
    }

    private void CdpService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ICdpService.IsConnected))
        {
            if (_cdpService.IsConnected)
            {
                _ = InitializeDomainAsync();
            }
            else
            {
                ClearData();
            }
        }
    }

    private async Task InitializeDomainAsync()
    {
        try
        {
            await _cdpService.SendCommandAsync("Network.enable");
            await ApplyThrottlingAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error enabling Network domain: {ex.Message}");
        }
    }

    private void CdpService_EventReceived(object? sender, CdpEventEventArgs e)
    {
        if (e.Method == "Network.requestWillBeSent" && e.Params != null)
        {
            string requestId = e.Params["requestId"]?.GetValue<string>() ?? "";
            var request = e.Params["request"] as JsonObject;
            if (request != null && !string.IsNullOrEmpty(requestId))
            {
                string url = request["url"]?.GetValue<string>() ?? "";
                string method = request["method"]?.GetValue<string>() ?? "GET";
                
                var reqHeaders = request["headers"] as JsonObject;
                var sbHeaders = new StringBuilder();
                if (reqHeaders != null)
                {
                    foreach (var header in reqHeaders)
                    {
                        sbHeaders.AppendLine($"{header.Key}: {header.Value}");
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    var existing = NetworkRequests.FirstOrDefault(r => r.RequestId == requestId);
                    if (existing == null)
                    {
                        NetworkRequests.Add(new NetworkRequestModel
                        {
                            RequestId = requestId,
                            Url = url,
                            Method = method,
                            Status = "Pending",
                            Time = "--",
                            RequestHeaders = sbHeaders.ToString()
                        });
                        if (NetworkRequests.Count > 100) NetworkRequests.RemoveAt(0);
                    }
                });
            }
        }
        else if (e.Method == "Network.responseReceived" && e.Params != null)
        {
            string requestId = e.Params["requestId"]?.GetValue<string>() ?? "";
            var response = e.Params["response"] as JsonObject;
            if (response != null && !string.IsNullOrEmpty(requestId))
            {
                int status = response["status"]?.GetValue<int>() ?? 200;
                string statusText = response["statusText"]?.GetValue<string>() ?? "OK";
                
                var resHeaders = response["headers"] as JsonObject;
                var sbHeaders = new StringBuilder();
                if (resHeaders != null)
                {
                    foreach (var header in resHeaders)
                    {
                        sbHeaders.AppendLine($"{header.Key}: {header.Value}");
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    var existing = NetworkRequests.FirstOrDefault(r => r.RequestId == requestId);
                    if (existing != null)
                    {
                        existing.Status = $"{status} {statusText}";
                        existing.ResponseHeaders = sbHeaders.ToString();
                        if (SelectedRequest == existing)
                        {
                            UpdateSelectedRequestDetails();
                        }
                    }
                });
            }
        }
        else if (e.Method == "Network.loadingFinished" && e.Params != null)
        {
            string requestId = e.Params["requestId"]?.GetValue<string>() ?? "";
            Dispatcher.UIThread.Post(() =>
            {
                var existing = NetworkRequests.FirstOrDefault(r => r.RequestId == requestId);
                if (existing != null)
                {
                    existing.Time = "Finished";
                    _ = FetchResponseBodyAsync(existing);
                }
            });
        }
    }

    private async Task FetchResponseBodyAsync(NetworkRequestModel req)
    {
        try
        {
            var p = new JsonObject { ["requestId"] = req.RequestId };
            var response = await _cdpService.SendCommandAsync("Network.getResponseBody", p);
            if (response != null)
            {
                string body = response["body"]?.GetValue<string>() ?? "";
                Dispatcher.UIThread.Post(() =>
                {
                    req.ResponseBody = body;
                    if (SelectedRequest == req)
                    {
                        SelectedResponseBody = body;
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching response body: {ex.Message}");
        }
    }

    private void ClearNetwork()
    {
        NetworkRequests.Clear();
        SelectedRequest = null;
    }

    private void ClearData()
    {
        Dispatcher.UIThread.Post(() =>
        {
            NetworkRequests.Clear();
            SelectedRequest = null;
        });
    }

    private void UpdateSelectedRequestDetails()
    {
        if (SelectedRequest != null)
        {
            SelectedUrl = SelectedRequest.Url;
            SelectedRequestHeaders = SelectedRequest.RequestHeaders;
            SelectedResponseHeaders = SelectedRequest.ResponseHeaders;
            SelectedResponseBody = SelectedRequest.ResponseBody;
        }
        else
        {
            SelectedUrl = "Select a request";
            SelectedRequestHeaders = "";
            SelectedResponseHeaders = "";
            SelectedResponseBody = "";
        }
    }

    private async Task ApplyThrottlingAsync()
    {
        if (SelectedProfile == null || !_cdpService.IsConnected) return;

        try
        {
            await _cdpService.SendCommandAsync("Network.emulateNetworkConditions", new JsonObject
            {
                ["offline"] = SelectedProfile.Offline,
                ["latency"] = SelectedProfile.Latency,
                ["downloadThroughput"] = SelectedProfile.DownloadThroughput,
                ["uploadThroughput"] = SelectedProfile.UploadThroughput
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying network throttling: {ex.Message}");
        }
    }
}

public class ThrottlingProfile
{
    public string DisplayName { get; }
    public bool Offline { get; }
    public double Latency { get; }
    public double DownloadThroughput { get; }
    public double UploadThroughput { get; }

    public ThrottlingProfile(string displayName, bool offline, double latency, double downloadThroughput, double uploadThroughput)
    {
        DisplayName = displayName;
        Offline = offline;
        Latency = latency;
        DownloadThroughput = downloadThroughput;
        UploadThroughput = uploadThroughput;
    }
}
