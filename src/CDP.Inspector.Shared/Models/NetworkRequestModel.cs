using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls.DataGridHierarchical;
using Microsoft.Extensions.Logging;
using Chrome.DevTools.Protocol;

namespace CdpInspectorApp.Models;

public class KeyValuePairModel
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public class JsonTreeNode
{
    public string Header { get; set; } = "";
    public ObservableCollection<JsonTreeNode>? Children { get; set; }
}

public class NetworkRequestModel : INotifyPropertyChanged
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger<NetworkRequestModel>();
    private string _url = "";
    private string _status = "Pending";
    private string _time = "--";
    private string _responseHeaders = "";
    private string _responseBody = "";
    private string _type = "XHR";
    private bool _base64Encoded;
    private ObservableCollection<KeyValuePairModel> _queryParameters = new();
    private ObservableCollection<KeyValuePairModel> _postParameters = new();
    private ObservableCollection<JsonTreeNode>? _jsonTree;
    private HierarchicalModel<JsonTreeNode>? _hierarchicalJsonTree;
    private Avalonia.Media.Imaging.Bitmap? _responseImage;
    private double _startTime;
    private double _endTime;
    private double _startOffset;
    private double _duration;
    private Thickness _waterfallMargin = new Thickness(0);
    private double _waterfallWidth = 1.0;
    private double _responseReceivedTime;
    private double _ttfbDuration;
    private double _downloadDuration;
    private double _startOffsetPercent;
    private double _ttfbPercent;
    private double _downloadPercent;

    public string RequestId { get; set; } = "";
    public string Method { get; set; } = "";
    public string RequestHeaders { get; set; } = "";

    public string Url
    {
        get => _url;
        set
        {
            _url = value;
            OnPropertyChanged(nameof(Url));
            ParseQueryString();
        }
    }

    public string Type
    {
        get => _type;
        set { _type = value; OnPropertyChanged(nameof(Type)); }
    }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(nameof(Status)); }
    }

    public string Time
    {
        get => _time;
        set { _time = value; OnPropertyChanged(nameof(Time)); }
    }

    public string ResponseHeaders
    {
        get => _responseHeaders;
        set { _responseHeaders = value; OnPropertyChanged(nameof(ResponseHeaders)); }
    }

    public string ResponseBody
    {
        get => _responseBody;
        set
        {
            _responseBody = value;
            OnPropertyChanged(nameof(ResponseBody));
            OnPropertyChanged(nameof(IsRawText));
            ParseJsonBody();
        }
    }

    public bool Base64Encoded
    {
        get => _base64Encoded;
        set { _base64Encoded = value; OnPropertyChanged(nameof(Base64Encoded)); }
    }

    public ObservableCollection<KeyValuePairModel> QueryParameters
    {
        get => _queryParameters;
        set { _queryParameters = value; OnPropertyChanged(nameof(QueryParameters)); OnPropertyChanged(nameof(HasQueryParameters)); OnPropertyChanged(nameof(HasPayload)); }
    }

    public ObservableCollection<KeyValuePairModel> PostParameters
    {
        get => _postParameters;
        set { _postParameters = value; OnPropertyChanged(nameof(PostParameters)); OnPropertyChanged(nameof(HasPostParameters)); OnPropertyChanged(nameof(HasPayload)); }
    }

    public ObservableCollection<JsonTreeNode>? JsonTree
    {
        get => _jsonTree;
        set
        {
            _jsonTree = value;
            OnPropertyChanged(nameof(JsonTree));
            OnPropertyChanged(nameof(IsJson));
            OnPropertyChanged(nameof(IsRawText));

            if (value == null)
            {
                HierarchicalJsonTree = null;
            }
            else
            {
                var options = new HierarchicalOptions<JsonTreeNode>
                {
                    ChildrenSelector = node => node.Children,
                    IsLeafSelector = node => node.Children == null || node.Children.Count == 0,
                    AutoExpandRoot = false
                };
                var model = new HierarchicalModel<JsonTreeNode>(options);
                model.SetRoots(value);
                HierarchicalJsonTree = model;
            }
        }
    }

    public HierarchicalModel<JsonTreeNode>? HierarchicalJsonTree
    {
        get => _hierarchicalJsonTree;
        set
        {
            _hierarchicalJsonTree = value;
            OnPropertyChanged(nameof(HierarchicalJsonTree));
        }
    }

    public Avalonia.Media.Imaging.Bitmap? ResponseImage
    {
        get => _responseImage;
        set
        {
            _responseImage = value;
            OnPropertyChanged(nameof(ResponseImage));
            OnPropertyChanged(nameof(IsImage));
            OnPropertyChanged(nameof(IsRawText));
        }
    }

    public double StartTime
    {
        get => _startTime;
        set
        {
            _startTime = value;
            OnPropertyChanged(nameof(StartTime));
        }
    }

    public double EndTime
    {
        get => _endTime;
        set
        {
            _endTime = value;
            OnPropertyChanged(nameof(EndTime));
        }
    }

    public double StartOffset
    {
        get => _startOffset;
        set
        {
            _startOffset = value;
            OnPropertyChanged(nameof(StartOffset));
        }
    }

    public double Duration
    {
        get => _duration;
        set
        {
            _duration = value;
            OnPropertyChanged(nameof(Duration));
        }
    }

    public Thickness WaterfallMargin
    {
        get => _waterfallMargin;
        set
        {
            _waterfallMargin = value;
            OnPropertyChanged(nameof(WaterfallMargin));
        }
    }

    public double WaterfallWidth
    {
        get => _waterfallWidth;
        set
        {
            _waterfallWidth = value;
            OnPropertyChanged(nameof(WaterfallWidth));
        }
    }

    public double ResponseReceivedTime
    {
        get => _responseReceivedTime;
        set
        {
            _responseReceivedTime = value;
            OnPropertyChanged(nameof(ResponseReceivedTime));
        }
    }

    public double TtfbDuration
    {
        get => _ttfbDuration;
        set
        {
            _ttfbDuration = value;
            OnPropertyChanged(nameof(TtfbDuration));
        }
    }

    public double DownloadDuration
    {
        get => _downloadDuration;
        set
        {
            _downloadDuration = value;
            OnPropertyChanged(nameof(DownloadDuration));
        }
    }

    public double StartOffsetPercent
    {
        get => _startOffsetPercent;
        set
        {
            _startOffsetPercent = value;
            OnPropertyChanged(nameof(StartOffsetPercent));
        }
    }

    public double TtfbPercent
    {
        get => _ttfbPercent;
        set
        {
            _ttfbPercent = value;
            OnPropertyChanged(nameof(TtfbPercent));
        }
    }

    public double DownloadPercent
    {
        get => _downloadPercent;
        set
        {
            _downloadPercent = value;
            OnPropertyChanged(nameof(DownloadPercent));
        }
    }

    public string TtfbDurationText => FormatTime(TtfbDuration);
    public string DownloadDurationText => FormatTime(DownloadDuration);
    public string TotalDurationText => FormatTime(Duration);

    public double TtfbPercentOfRequest => Duration > 0 ? TtfbDuration / Duration : 0.0;
    public double DownloadPercentOfRequest => Duration > 0 ? DownloadDuration / Duration : 0.0;

    public string WaterfallToolTip => $"Start offset: {FormatTime(StartOffset)}\nWaiting (TTFB): {FormatTime(TtfbDuration)}\nContent Download: {FormatTime(DownloadDuration)}\nTotal: {FormatTime(Duration)}";

    private static string FormatTime(double seconds)
    {
        return seconds >= 1.0 
            ? seconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " s" 
            : (seconds * 1000).ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " ms";
    }

    public void UpdateTimeline(double sessionStartTime, double totalDuration)
    {
        StartOffset = StartTime - sessionStartTime;
        if (StartOffset < 0) StartOffset = 0;

        Duration = EndTime >= StartTime ? EndTime - StartTime : 0;

        TtfbDuration = ResponseReceivedTime >= StartTime ? ResponseReceivedTime - StartTime : Duration;
        DownloadDuration = EndTime >= ResponseReceivedTime ? EndTime - ResponseReceivedTime : 0;

        double offsetPercent = totalDuration > 0 ? StartOffset / totalDuration : 0;
        double ttfbPercent = totalDuration > 0 ? TtfbDuration / totalDuration : 0;
        double downloadPercent = totalDuration > 0 ? DownloadDuration / totalDuration : 0;

        StartOffsetPercent = Math.Max(0.0, Math.Min(1.0, offsetPercent));
        TtfbPercent = Math.Max(0.0, Math.Min(1.0, ttfbPercent));
        DownloadPercent = Math.Max(0.0, Math.Min(1.0, downloadPercent));

        if (StartOffsetPercent + TtfbPercent + DownloadPercent > 1.0)
        {
            if (StartOffsetPercent + TtfbPercent > 1.0)
            {
                TtfbPercent = 1.0 - StartOffsetPercent;
                DownloadPercent = 0.0;
            }
            else
            {
                DownloadPercent = 1.0 - (StartOffsetPercent + TtfbPercent);
            }
        }

        const double targetWidth = 120.0;
        double leftMargin = StartOffsetPercent * targetWidth;
        double barWidth = (TtfbPercent + DownloadPercent) * targetWidth;

        if (barWidth < 3.0)
        {
            barWidth = 3.0;
        }

        if (leftMargin + barWidth > targetWidth)
        {
            if (leftMargin > targetWidth - 3.0)
            {
                leftMargin = targetWidth - 3.0;
            }
            barWidth = targetWidth - leftMargin;
        }

        WaterfallMargin = new Thickness(leftMargin, 0, 0, 0);
        WaterfallWidth = barWidth;

        OnPropertyChanged(nameof(WaterfallToolTip));
        OnPropertyChanged(nameof(TtfbDurationText));
        OnPropertyChanged(nameof(DownloadDurationText));
        OnPropertyChanged(nameof(TotalDurationText));
        OnPropertyChanged(nameof(TtfbPercentOfRequest));
        OnPropertyChanged(nameof(DownloadPercentOfRequest));
    }

    public bool HasPayload => QueryParameters.Count > 0 || PostParameters.Count > 0;
    public bool HasQueryParameters => QueryParameters.Count > 0;
    public bool HasPostParameters => PostParameters.Count > 0;
    public bool IsJson => JsonTree != null && JsonTree.Count > 0;
    public bool IsImage => ResponseImage != null;
    public bool IsRawText => !IsJson && !IsImage && !string.IsNullOrEmpty(ResponseBody);

    private void ParseQueryString()
    {
        var list = new ObservableCollection<KeyValuePairModel>();
        try
        {
            if (!string.IsNullOrEmpty(_url))
            {
                int qIndex = _url.IndexOf('?');
                if (qIndex >= 0 && qIndex < _url.Length - 1)
                {
                    string queryString = _url.Substring(qIndex + 1);
                    int hashIndex = queryString.IndexOf('#');
                    if (hashIndex >= 0)
                    {
                        queryString = queryString.Substring(0, hashIndex);
                    }

                    var parts = queryString.Split('&');
                    foreach (var part in parts)
                    {
                        if (string.IsNullOrEmpty(part)) continue;
                        var pair = part.Split('=', 2);
                        string key = Uri.UnescapeDataString(pair[0]);
                        string value = pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : "";
                        list.Add(new KeyValuePairModel { Key = key, Value = value });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("NetworkRequestModel", "Error parsing query string", ex);
        }
        QueryParameters = list;
    }
 
    public void ParsePostParameters(string? postData)
    {
        var list = new ObservableCollection<KeyValuePairModel>();
        if (string.IsNullOrEmpty(postData))
        {
            PostParameters = list;
            return;
        }
 
        try
        {
            string trimmed = postData.Trim();
            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
            {
                var node = JsonNode.Parse(trimmed);
                if (node is JsonObject obj)
                {
                    foreach (var kvp in obj)
                    {
                        list.Add(new KeyValuePairModel
                        {
                            Key = kvp.Key,
                            Value = kvp.Value?.ToString() ?? "null"
                        });
                    }
                }
                else if (node is JsonArray arr)
                {
                    for (int i = 0; i < arr.Count; i++)
                    {
                        list.Add(new KeyValuePairModel
                        {
                            Key = $"[{i}]",
                            Value = arr[i]?.ToString() ?? "null"
                        });
                    }
                }
            }
            else
            {
                if (trimmed.Contains('=') || trimmed.Contains('&'))
                {
                    var parts = trimmed.Split('&');
                    foreach (var part in parts)
                    {
                        if (string.IsNullOrEmpty(part)) continue;
                        var pair = part.Split('=', 2);
                        string key = Uri.UnescapeDataString(pair[0]);
                        string value = pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : "";
                        list.Add(new KeyValuePairModel { Key = key, Value = value });
                    }
                }
                else
                {
                    list.Add(new KeyValuePairModel { Key = "raw", Value = trimmed });
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("NetworkRequestModel", "Error parsing post data", ex);
            list.Add(new KeyValuePairModel { Key = "raw", Value = postData });
        }
        PostParameters = list;
    }

    private void ParseJsonBody()
    {
        if (string.IsNullOrEmpty(_responseBody))
        {
            JsonTree = null;
            return;
        }

        try
        {
            var trimmed = _responseBody.Trim();
            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
            {
                var node = JsonNode.Parse(_responseBody);
                if (node != null)
                {
                    JsonTree = BuildJsonTree(node);
                }
                else
                {
                    JsonTree = null;
                }
            }
            else
            {
                JsonTree = null;
            }
        }
        catch
        {
            JsonTree = null;
        }
    }

    private static ObservableCollection<JsonTreeNode> BuildJsonTree(JsonNode? node, string name = "")
    {
        var list = new ObservableCollection<JsonTreeNode>();
        if (node == null) return list;

        if (node is JsonObject obj)
        {
            var children = new ObservableCollection<JsonTreeNode>();
            foreach (var kvp in obj)
            {
                var childNodes = BuildJsonTree(kvp.Value, kvp.Key);
                foreach (var child in childNodes)
                {
                    children.Add(child);
                }
            }
            list.Add(new JsonTreeNode
            {
                Header = string.IsNullOrEmpty(name) ? "{}" : $"{name}: {{}}",
                Children = children
            });
        }
        else if (node is JsonArray arr)
        {
            var children = new ObservableCollection<JsonTreeNode>();
            for (int i = 0; i < arr.Count; i++)
            {
                var childNodes = BuildJsonTree(arr[i], $"[{i}]");
                foreach (var child in childNodes)
                {
                    children.Add(child);
                }
            }
            list.Add(new JsonTreeNode
            {
                Header = string.IsNullOrEmpty(name) ? "[]" : $"{name}: []",
                Children = children
            });
        }
        else
        {
            list.Add(new JsonTreeNode
            {
                Header = string.IsNullOrEmpty(name) ? node.ToString() : $"{name}: {node}"
            });
        }
        return list;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
