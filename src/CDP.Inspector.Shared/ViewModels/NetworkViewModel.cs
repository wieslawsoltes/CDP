using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows.Input;
using System.Collections.Specialized;
using Avalonia.Threading;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;
using Avalonia.Layout;
using CDP.Editor.Splits.Models;

namespace CdpInspectorApp.ViewModels;

public class NetworkViewModel : ViewModelBase, IStateProvider
{
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
    private readonly ICdpService _cdpService;
    private ObservableCollection<NetworkRequestModel> _networkRequests = new();
    private NetworkRequestModel? _selectedRequest;
    private string _selectedUrl = "Select a request";
    private string _selectedRequestHeaders = "";
    private string _selectedResponseHeaders = "";
    private string _selectedResponseBody = "";
    private double _sessionStartTime = 0.0;

    private ObservableCollection<ThrottlingProfile> _throttlingProfiles = new()
    {
        new ThrottlingProfile("No Throttling", false, 0, -1, -1),
        new ThrottlingProfile("Fast 3G", false, 100, 200000, 96000),
        new ThrottlingProfile("Slow 3G", false, 400, 47000, 47000),
        new ThrottlingProfile("Offline", true, 0, 0, 0)
    };
    private ThrottlingProfile? _selectedProfile;

    private ObservableCollection<BlockedUrlModel> _blockedUrls = new();
    private ObservableCollection<MockRuleModel> _mockRules = new();
    private MockRuleModel? _selectedMockRule;

    private string _activeFilter = "All";
    public ObservableCollection<string> Filters { get; } = new() { "All", "Fetch/XHR", "CSS/JS", "Images", "Doc", "Other" };

    public ObservableCollection<NetworkRequestModel> NetworkRequests => _networkRequests;
    public ObservableCollection<ThrottlingProfile> ThrottlingProfiles => _throttlingProfiles;
    public ObservableCollection<BlockedUrlModel> BlockedUrls => _blockedUrls;
    public ObservableCollection<MockRuleModel> MockRules => _mockRules;

    public ICommand AddBlockedUrlCommand { get; }
    public ICommand RemoveBlockedUrlCommand { get; }
    public ICommand AddMockRuleCommand { get; }
    public ICommand RemoveMockRuleCommand { get; }

    private string _filterText = "";

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (RaiseAndSetIfChanged(ref _filterText, value))
            {
                OnPropertyChanged(nameof(FilteredNetworkRequests));
                OnPropertyChanged(nameof(FilteredRequestsCount));
            }
        }
    }

    public string ActiveFilter
    {
        get => _activeFilter;
        set
        {
            if (RaiseAndSetIfChanged(ref _activeFilter, value))
            {
                OnPropertyChanged(nameof(FilteredNetworkRequests));
                OnPropertyChanged(nameof(FilteredRequestsCount));
            }
        }
    }

    public System.Collections.Generic.IEnumerable<NetworkRequestModel> FilteredNetworkRequests
    {
        get
        {
            var requests = NetworkRequests.AsEnumerable();

            if (!string.IsNullOrEmpty(FilterText))
            {
                requests = requests.Where(r => r.Url.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
            }

            if (string.IsNullOrEmpty(ActiveFilter) || ActiveFilter == "All")
            {
                return requests;
            }

            return requests.Where(r =>
            {
                string type = r.Type.ToLowerInvariant();
                return ActiveFilter switch
                {
                    "Fetch/XHR" => type == "xhr" || type == "fetch",
                    "CSS/JS" => type == "stylesheet" || type == "script",
                    "Images" => type == "image",
                    "Doc" => type == "document",
                    "Other" => type != "xhr" && type != "fetch" && type != "stylesheet" && type != "script" && type != "image" && type != "document",
                    _ => true
                };
            });
        }
    }

    public int FilteredRequestsCount => FilteredNetworkRequests.Count();

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

    public DiffViewModel SelectedRequestHeaderDiff { get; } = new();

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

    public MockRuleModel? SelectedMockRule
    {
        get => _selectedMockRule;
        set => RaiseAndSetIfChanged(ref _selectedMockRule, value);
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

    public bool AutoFulfillEnabled { get; set; } = true;

    public ICommand ClearNetworkCommand { get; }

    public NetworkViewModel(ICdpService cdpService)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _cdpService.PropertyChanged += CdpService_PropertyChanged;
        _cdpService.EventReceived += CdpService_EventReceived;
        _cdpService.TimeMachine.ReplayStateCleared += (sender, args) => ClearNetwork();

        _selectedProfile = _throttlingProfiles[0];
        ClearNetworkCommand = new RelayCommand(ClearNetwork);

        AddBlockedUrlCommand = new RelayCommand(AddBlockedUrl);
        RemoveBlockedUrlCommand = new RelayCommand<BlockedUrlModel>(RemoveBlockedUrl);
        AddMockRuleCommand = new RelayCommand(AddMockRule);
        RemoveMockRuleCommand = new RelayCommand<MockRuleModel>(RemoveMockRule);

        _networkRequests.CollectionChanged += (s, e) => { OnPropertyChanged(nameof(FilteredNetworkRequests)); OnPropertyChanged(nameof(FilteredRequestsCount)); };
        ResetLayout();
    }

    public void ResetLayout()
    {
        var left = new BoxNode();
        left.AddTab("Requests List", "TableIcon", "RequestsList");

        var right = new BoxNode();
        right.AddTab("Request Details", "CodeIcon", "RequestDetails");
        right.AddTab("Mocking Rules", "SettingsIcon", "MockingRules");

        LayoutRoot = new SplitContainerNode(Orientation.Horizontal, left, right) { SplitterRatio = 0.55 };
        SelectedPane = left;
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
            await _cdpService.SendCommandAsync("Fetch.enable");
            await ApplyThrottlingAsync();
            await SyncBlockedUrlsAsync();
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
                string type = e.Params["type"]?.GetValue<string>() ?? "";
                type = DetermineType(url, type);
                double timestamp = e.Params["timestamp"]?.GetValue<double>() ?? 0.0;
                
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
                        string initialStatus = "Pending";
                        if (BlockedUrls.Any(b => MatchesPattern(url, b.Pattern)))
                        {
                            initialStatus = "Blocked";
                        }
                        else if (MockRules.Any(r => r.IsActive && MatchesPattern(url, r.UrlPattern)))
                        {
                            initialStatus = "Mocked";
                        }

                        if (_sessionStartTime == 0.0 && timestamp > 0.0)
                        {
                            _sessionStartTime = timestamp;
                        }

                        var model = new NetworkRequestModel
                        {
                            RequestId = requestId,
                            Url = url,
                            Method = method,
                            Type = type,
                            Status = initialStatus,
                            Time = "--",
                            RequestHeaders = sbHeaders.ToString(),
                            StartTime = timestamp > 0.0 ? timestamp : (_sessionStartTime > 0.0 ? _sessionStartTime : 0.0),
                            EndTime = timestamp > 0.0 ? timestamp : (_sessionStartTime > 0.0 ? _sessionStartTime : 0.0)
                        };
                        string? postData = request["postData"]?.GetValue<string>();
                        model.ParsePostParameters(postData);

                        NetworkRequests.Add(model);
                        if (NetworkRequests.Count > 100) NetworkRequests.RemoveAt(0);

                        RecalculateWaterfallTimelines();
                    }
                });
            }
        }
        else if (e.Method == "Network.responseReceived" && e.Params != null)
        {
            string requestId = e.Params["requestId"]?.GetValue<string>() ?? "";
            var response = e.Params["response"] as JsonObject;
            string type = e.Params["type"]?.GetValue<string>() ?? "";
            double timestamp = e.Params["timestamp"]?.GetValue<double>() ?? 0.0;
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
                        string finalStatus = $"{status} {statusText}";
                        if (BlockedUrls.Any(b => MatchesPattern(existing.Url, b.Pattern)))
                        {
                            finalStatus = "Blocked";
                        }
                        else if (MockRules.Any(r => r.IsActive && MatchesPattern(existing.Url, r.UrlPattern)))
                        {
                            finalStatus = "Mocked";
                        }

                        existing.Status = finalStatus;
                        existing.ResponseHeaders = sbHeaders.ToString();
                        if (!string.IsNullOrEmpty(type))
                        {
                            existing.Type = type;
                        }
                        existing.ResponseReceivedTime = timestamp;
                        if (timestamp > 0.0)
                        {
                            existing.EndTime = timestamp;
                            double durationSec = existing.EndTime - existing.StartTime;
                            if (durationSec < 0) durationSec = 0;
                            existing.Time = durationSec >= 1.0 
                                ? $"{durationSec:F2} s" 
                                : $"{durationSec * 1000:F0} ms";
                        }
                        if (SelectedRequest == existing)
                        {
                            UpdateSelectedRequestDetails();
                        }

                        RecalculateWaterfallTimelines();
                    }
                });
            }
        }
        else if (e.Method == "Network.loadingFinished" && e.Params != null)
        {
            string requestId = e.Params["requestId"]?.GetValue<string>() ?? "";
            double timestamp = e.Params["timestamp"]?.GetValue<double>() ?? 0.0;
            Dispatcher.UIThread.Post(() =>
            {
                var existing = NetworkRequests.FirstOrDefault(r => r.RequestId == requestId);
                if (existing != null)
                {
                    if (timestamp > 0.0)
                    {
                        existing.EndTime = timestamp;
                        double durationSec = existing.EndTime - existing.StartTime;
                        if (durationSec < 0) durationSec = 0;
                        existing.Time = durationSec >= 1.0 
                            ? $"{durationSec:F2} s" 
                            : $"{durationSec * 1000:F0} ms";
                    }
                    else
                    {
                        existing.Time = "Finished";
                    }
                    _ = FetchResponseBodyAsync(existing);

                    RecalculateWaterfallTimelines();
                }
            });
        }
        else if (e.Method == "Network.loadingFailed" && e.Params != null)
        {
            string requestId = e.Params["requestId"]?.GetValue<string>() ?? "";
            double timestamp = e.Params["timestamp"]?.GetValue<double>() ?? 0.0;
            string errorText = e.Params["errorText"]?.GetValue<string>() ?? "Failed";
            Dispatcher.UIThread.Post(() =>
            {
                var existing = NetworkRequests.FirstOrDefault(r => r.RequestId == requestId);
                if (existing != null)
                {
                    if (timestamp > 0.0)
                    {
                        existing.EndTime = timestamp;
                        double durationSec = existing.EndTime - existing.StartTime;
                        if (durationSec < 0) durationSec = 0;
                        existing.Time = durationSec >= 1.0 
                            ? $"{durationSec:F2} s" 
                            : $"{durationSec * 1000:F0} ms";
                    }
                    else
                    {
                        existing.Time = "Failed";
                    }
                    existing.Status = errorText;

                    RecalculateWaterfallTimelines();
                }
            });
        }
        else if (e.Method == "Fetch.requestPaused" && e.Params != null)
        {
            if (!AutoFulfillEnabled) return;
            string requestId = e.Params["requestId"]?.GetValue<string>() ?? "";
            var request = e.Params["request"] as JsonObject;
            if (request != null && !string.IsNullOrEmpty(requestId))
            {
                string url = request["url"]?.GetValue<string>() ?? "";
                string method = request["method"]?.GetValue<string>() ?? "GET";

                var matchingRule = MockRules.FirstOrDefault(r => r.IsActive && MatchesPattern(url, r.UrlPattern));
                if (matchingRule != null)
                {
                    _ = FulfillPausedRequestAsync(requestId, matchingRule);
                }
                else
                {
                    _ = ContinuePausedRequestAsync(requestId);
                }
            }
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
                bool base64Encoded = response["base64Encoded"]?.GetValue<bool>() ?? false;
                Dispatcher.UIThread.Post(() =>
                {
                    req.Base64Encoded = base64Encoded;
                    req.ResponseBody = body;
                    if (req.Type.Equals("Image", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            byte[]? bytes = null;
                            if (base64Encoded)
                            {
                                bytes = Convert.FromBase64String(body);
                            }
                            else
                            {
                                // Fallback: try decoding as base64 first (handling backends that omit the flag)
                                try
                                {
                                    bytes = Convert.FromBase64String(body);
                                }
                                catch
                                {
                                    // Treat ISO-8859-1 (Latin1) to map 1-to-1 back to byte values from raw string
                                    bytes = System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(body);
                                }
                            }

                            if (bytes != null)
                            {
                                using (var ms = new System.IO.MemoryStream(bytes))
                                {
                                    req.ResponseImage = new Avalonia.Media.Imaging.Bitmap(ms);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error decoding image: {ex.Message}");
                        }
                    }
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
        _sessionStartTime = 0.0;
    }

    private void ClearData()
    {
        Dispatcher.UIThread.Post(() =>
        {
            NetworkRequests.Clear();
            SelectedRequest = null;
            _sessionStartTime = 0.0;
        });
    }

    private void RecalculateWaterfallTimelines()
    {
        if (NetworkRequests.Count == 0) return;

        double sessionStartTime = _sessionStartTime;
        if (sessionStartTime == 0.0)
        {
            sessionStartTime = NetworkRequests.Min(r => r.StartTime);
        }

        double sessionEndTime = NetworkRequests.Max(r => Math.Max(r.StartTime, r.EndTime));
        double totalDuration = sessionEndTime - sessionStartTime;
        if (totalDuration <= 0) totalDuration = 1.0;

        foreach (var req in NetworkRequests)
        {
            req.UpdateTimeline(sessionStartTime, totalDuration);
        }
    }

    private void UpdateSelectedRequestDetails()
    {
        if (SelectedRequest != null)
        {
            SelectedUrl = SelectedRequest.Url;
            SelectedRequestHeaders = SelectedRequest.RequestHeaders;
            SelectedResponseHeaders = SelectedRequest.ResponseHeaders;
            SelectedResponseBody = SelectedRequest.ResponseBody;
            SelectedRequestHeaderDiff.SetCompareTexts(
                "Request Headers",
                SelectedRequest.RequestHeaders,
                "Response Headers",
                SelectedRequest.ResponseHeaders
            );
        }
        else
        {
            SelectedUrl = "Select a request";
            SelectedRequestHeaders = "";
            SelectedResponseHeaders = "";
            SelectedResponseBody = "";
            SelectedRequestHeaderDiff.SetCompareTexts("Request Headers", "", "Response Headers", "");
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

    private void AddBlockedUrl()
    {
        var item = new BlockedUrlModel { Pattern = "*google-analytics.com*" };
        item.PropertyChanged += BlockedUrl_PropertyChanged;
        BlockedUrls.Add(item);
        _ = SyncBlockedUrlsAsync();
    }

    private void RemoveBlockedUrl(BlockedUrlModel? item)
    {
        if (item != null)
        {
            item.PropertyChanged -= BlockedUrl_PropertyChanged;
            BlockedUrls.Remove(item);
            _ = SyncBlockedUrlsAsync();
        }
    }

    private void BlockedUrl_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BlockedUrlModel.Pattern))
        {
            _ = SyncBlockedUrlsAsync();
        }
    }

    private void AddMockRule()
    {
        var rule = new MockRuleModel
        {
            UrlPattern = "*api/v1/users*",
            StatusCode = 200,
            MockBody = "{\"mocked\": true}",
            ResponseHeaders = "Content-Type: application/json"
        };
        MockRules.Add(rule);
    }

    private void RemoveMockRule(MockRuleModel? rule)
    {
        if (rule != null)
        {
            MockRules.Remove(rule);
        }
    }

    private async Task SyncBlockedUrlsAsync()
    {
        if (!_cdpService.IsConnected) return;

        try
        {
            var urlsArray = new JsonArray();
            foreach (var blocked in BlockedUrls)
            {
                if (!string.IsNullOrEmpty(blocked.Pattern))
                {
                    urlsArray.Add((JsonNode?)JsonValue.Create(blocked.Pattern));
                }
            }

            await _cdpService.SendCommandAsync("Network.setBlockedURLs", new JsonObject
            {
                ["urls"] = urlsArray
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating blocked URLs: {ex.Message}");
        }
    }

    private async Task FulfillPausedRequestAsync(string requestId, MockRuleModel rule)
    {
        try
        {
            var headersArray = new JsonArray();
            if (!string.IsNullOrEmpty(rule.ResponseHeaders))
            {
                var lines = rule.ResponseHeaders.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var parts = line.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        headersArray.Add((JsonNode)new JsonObject
                        {
                            ["name"] = parts[0].Trim(),
                            ["value"] = parts[1].Trim()
                        });
                    }
                }
            }

            string base64Body = "";
            if (!string.IsNullOrEmpty(rule.MockBody))
            {
                base64Body = Convert.ToBase64String(Encoding.UTF8.GetBytes(rule.MockBody));
            }

            await _cdpService.SendCommandAsync("Fetch.fulfillRequest", new JsonObject
            {
                ["requestId"] = requestId,
                ["responseCode"] = rule.StatusCode,
                ["responseHeaders"] = headersArray,
                ["body"] = base64Body
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fulfilling request: {ex.Message}");
        }
    }

    private async Task ContinuePausedRequestAsync(string requestId)
    {
        try
        {
            await _cdpService.SendCommandAsync("Fetch.continueRequest", new JsonObject
            {
                ["requestId"] = requestId
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error continuing request: {ex.Message}");
        }
    }

    private bool MatchesPattern(string url, string pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return false;
        if (pattern == "*") return true;

        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(url, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string DetermineType(string url, string? cdpType)
    {
        bool isPlaceholder = string.IsNullOrEmpty(cdpType) || 
                             string.Equals(cdpType, "xhr", StringComparison.OrdinalIgnoreCase) || 
                             string.Equals(cdpType, "fetch", StringComparison.OrdinalIgnoreCase);

        if (!isPlaceholder && !string.IsNullOrEmpty(cdpType)) return cdpType;

        if (!string.IsNullOrEmpty(url))
        {
            try
            {
                var uri = new Uri(url);
                var path = uri.AbsolutePath.ToLowerInvariant();
                if (path.EndsWith(".js") || path.EndsWith(".mjs")) return "Script";
                if (path.EndsWith(".css")) return "Stylesheet";
                if (path.EndsWith(".png") || path.EndsWith(".jpg") || path.EndsWith(".jpeg") || path.EndsWith(".gif") || path.EndsWith(".svg") || path.EndsWith(".webp") || path.EndsWith(".ico")) return "Image";
                if (path.EndsWith(".html") || path.EndsWith(".htm")) return "Document";
            }
            catch
            {
                // Fallback
            }
        }

        return !string.IsNullOrEmpty(cdpType) ? cdpType : "XHR";
    }

    #region IStateProvider Implementation

    public string StateKey => "network";

    public JsonNode? SaveState()
    {
        var root = new JsonObject();
        root["selectedProfileName"] = SelectedProfile?.DisplayName;
        root["activeFilter"] = ActiveFilter;

        var blockedArray = new JsonArray();
        foreach (var blocked in BlockedUrls)
        {
            if (!string.IsNullOrEmpty(blocked.Pattern))
            {
                blockedArray.Add(blocked.Pattern);
            }
        }
        root["blockedUrls"] = blockedArray;

        var mockRulesArray = new JsonArray();
        foreach (var rule in MockRules)
        {
            var ruleObj = new JsonObject
            {
                ["urlPattern"] = rule.UrlPattern,
                ["isActive"] = rule.IsActive,
                ["statusCode"] = rule.StatusCode,
                ["mockBody"] = rule.MockBody,
                ["responseHeaders"] = rule.ResponseHeaders
            };
            mockRulesArray.Add(ruleObj);
        }
        root["mockRules"] = mockRulesArray;

        return root;
    }

    public void LoadState(JsonNode? stateNode)
    {
        if (stateNode is not JsonObject json) return;

        if (json.TryGetPropertyValue("activeFilter", out var filterNode) && filterNode != null)
        {
            ActiveFilter = (string?)filterNode ?? "All";
        }

        if (json.TryGetPropertyValue("blockedUrls", out var blockedNode) && blockedNode is JsonArray blockedArray)
        {
            BlockedUrls.Clear();
            foreach (var item in blockedArray)
            {
                var pattern = (string?)item;
                if (!string.IsNullOrEmpty(pattern))
                {
                    var model = new BlockedUrlModel { Pattern = pattern };
                    model.PropertyChanged += BlockedUrl_PropertyChanged;
                    BlockedUrls.Add(model);
                }
            }
            _ = SyncBlockedUrlsAsync();
        }

        if (json.TryGetPropertyValue("mockRules", out var mockRulesNode) && mockRulesNode is JsonArray mockRulesArray)
        {
            MockRules.Clear();
            foreach (var item in mockRulesArray)
            {
                if (item is JsonObject ruleObj)
                {
                    var rule = new MockRuleModel
                    {
                        UrlPattern = (string?)ruleObj["urlPattern"] ?? "",
                        IsActive = (bool?)ruleObj["isActive"] ?? true,
                        StatusCode = (int?)ruleObj["statusCode"] ?? 200,
                        MockBody = (string?)ruleObj["mockBody"] ?? "",
                        ResponseHeaders = (string?)ruleObj["responseHeaders"] ?? "Content-Type: application/json"
                    };
                    MockRules.Add(rule);
                }
            }
        }

        if (json.TryGetPropertyValue("selectedProfileName", out var profileNode) && profileNode != null)
        {
            var profileName = (string?)profileNode;
            var matchedProfile = ThrottlingProfiles.FirstOrDefault(p => p.DisplayName == profileName);
            if (matchedProfile != null)
            {
                SelectedProfile = matchedProfile;
            }
        }
    }

    #endregion
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
