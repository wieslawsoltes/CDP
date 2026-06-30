using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia;
using Avalonia.Threading;
using CdpInspectorApp.Services;

using SkiaSharp;

namespace CdpInspectorApp.Views;

public class PlaybackFrame
{
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public double TimestampMs { get; set; }
}

public class PlaybackNetworkItemViewModel
{
    public string Method { get; }
    public string Url { get; }
    public string DisplayUrl
    {
        get
        {
            try
            {
                var uri = new Uri(Url);
                return uri.PathAndQuery;
            }
            catch
            {
                return Url;
            }
        }
    }
    public string Status { get; }
    public string StatusColor => (Status.StartsWith("4") || Status.StartsWith("5") || Status == "Failed") ? "#ef4444" :
                                 Status.StartsWith("3") ? "#f59e0b" : "#10b981";
    public double DurationMs { get; }
    public string DurationText => $"{DurationMs:F0} ms";

    public Thickness TimelineMargin { get; }
    public double TimelineWidth { get; }

    public PlaybackNetworkItemViewModel(NetworkReportItem item)
    {
        Method = item.Method;
        Url = item.Url;
        Status = item.Status;
        DurationMs = item.DurationMs;
        TimelineMargin = new Thickness(0);
        TimelineWidth = 0;
    }

    public PlaybackNetworkItemViewModel(NetworkReportItem item, double stepStartMs, double stepDurationMs)
    {
        Method = item.Method;
        Url = item.Url;
        Status = item.Status;
        DurationMs = item.DurationMs;

        double offsetMs = Math.Max(0, item.RelativeStartMs - stepStartMs);
        double stepDur = stepDurationMs <= 0 ? 1.0 : stepDurationMs;

        double offsetPct = Math.Min(1.0, offsetMs / stepDur);
        double durationPct = Math.Min(1.0 - offsetPct, item.DurationMs / stepDur);

        double totalWidth = 100.0;
        double left = offsetPct * totalWidth;
        double width = Math.Max(4.0, durationPct * totalWidth);

        if (left + width > totalWidth)
        {
            width = totalWidth - left;
        }

        TimelineMargin = new Thickness(left, 0, 0, 0);
        TimelineWidth = width;
    }
}

public class PlaybackStepViewModel
{
    public int Index { get; }
    public string DisplayIndex => $"#{Index}";
    public string Action { get; }
    public string ActionDisplay { get; }
    public string StatusText { get; }
    public string StatusFg => StatusText == "PASSED" ? "#10b981" : "#ef4444";
    public string StatusBg => StatusText == "PASSED" ? "#1e293b" : "#450a0a";
    public string Selector { get; }
    public string SelectorText => string.IsNullOrEmpty(Selector) ? "" : $"Selector: {Selector}";
    public string Value { get; }
    public string ValueText => string.IsNullOrEmpty(Value) ? "" : $"Value: {Value}";
    public double DurationMs { get; }
    public string DurationText => $"Duration: {DurationMs:F0} ms";
    public double RelativeStartMs { get; }
    public Bitmap? Screenshot { get; }

    public double CpuUsage { get; }
    public double MemoryJsHeapUsed { get; }
    public double MemoryJsHeapTotal { get; }
    public double Fps { get; }
    public int NetworkRequestCount { get; }
    public long NetworkResponseBytes { get; }
    public int DomNodes { get; }
    public int DomDocuments { get; }

    public StepReportItem Step { get; }

    public PlaybackStepViewModel(StepReportItem item, Bitmap? screenshot)
    {
        Step = item;
        Index = item.Index;
        Action = item.Action;
        ActionDisplay = item.ActionDisplay;
        StatusText = item.Status.ToUpper();
        Selector = item.Selector;
        Value = item.Value;
        DurationMs = item.DurationMs;
        RelativeStartMs = item.RelativeStartMs;
        Screenshot = screenshot;

        CpuUsage = item.CpuUsage;
        MemoryJsHeapUsed = item.MemoryJsHeapUsed;
        MemoryJsHeapTotal = item.MemoryJsHeapTotal;
        Fps = item.Fps;
        NetworkRequestCount = item.NetworkRequestCount;
        NetworkResponseBytes = item.NetworkResponseBytes;
        DomNodes = item.DomNodes;
        DomDocuments = item.DomDocuments;
    }
}

public partial class VideoPlaybackWindow : Window
{
    private List<PlaybackFrame> _frames = new();
    private readonly Dictionary<int, Bitmap> _bitmapCache = new();
    private readonly List<int> _cacheKeysOrder = new();
    private readonly List<PlaybackStepViewModel> _stepViewModels = new();
    
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = new();
    
    private List<RunMetricSample> _metricsTimeline = new();
    private List<NetworkReportItem> _networkRequests = new();
    private PlaybackStepViewModel? _lastUpdatedStep = null;
    private bool _isPlaying = false;
    private double _currentPositionMs = 0;
    private double _playbackSpeed = 1.0;
    private bool _isSeeking = false;
    private bool _isUpdatingSelectionFromPlayback = false;
    private double _totalDurationMs = 0;

    public VideoPlaybackWindow()
    {
        InitializeComponent();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60fps tick
        };
        _timer.Tick += Timer_Tick;

        // UI Event Handlers
        btnPlay.Click += BtnPlay_Click;
        btnStop.Click += BtnStop_Click;
        
        btnSpeed1x.Click += (s, e) => SetSpeed(1.0);
        btnSpeed2x.Click += (s, e) => SetSpeed(2.0);
        btnSpeed4x.Click += (s, e) => SetSpeed(4.0);

        sliderSeek.PropertyChanged += SliderSeek_PropertyChanged;
        listSteps.SelectionChanged += ListSteps_SelectionChanged;
        
        Closed += VideoPlaybackWindow_Closed;
        
        UpdateSpeedButtons();
    }

    public void SetFramesAndSteps(List<PlaybackFrame> frames, List<StepReportItem> steps, string? pdfReportPath)
    {
        SetFramesAndSteps(frames, steps, new List<RunMetricSample>(), new List<NetworkReportItem>(), pdfReportPath);
    }

    public void SetFramesAndSteps(
        List<PlaybackFrame> frames,
        List<StepReportItem> steps,
        List<RunMetricSample> metrics,
        List<NetworkReportItem> networkRequests,
        string? pdfReportPath)
    {
        _metricsTimeline = metrics ?? new List<RunMetricSample>();
        _networkRequests = networkRequests ?? new List<NetworkReportItem>();
        _lastUpdatedStep = null;

        // Set telemetry view dependencies
        stepTelemetry.Metrics = _metricsTimeline;
        stepTelemetry.Network = _networkRequests;

        _frames = frames ?? new List<PlaybackFrame>();
        
        // 1. Clear old cache
        foreach (var bmp in _bitmapCache.Values)
        {
            bmp.Dispose();
        }
        _bitmapCache.Clear();
        _cacheKeysOrder.Clear();

        // 2. Load and build step view models
        _stepViewModels.Clear();
        foreach (var step in steps)
        {
            Bitmap? bmp = null;
            if (!string.IsNullOrEmpty(step.ScreenshotFileName))
            {
                try
                {
                    // Check if it is base64 string
                    if (!step.ScreenshotFileName.Contains("/") && !step.ScreenshotFileName.Contains("\\") && step.ScreenshotFileName.Length > 100)
                    {
                        byte[] bytes = Convert.FromBase64String(step.ScreenshotFileName);
                        using var ms = new MemoryStream(bytes);
                        bmp = new Bitmap(ms);
                    }
                    else
                    {
                        // Resolve relative to report path
                        var path = step.ScreenshotFileName;
                        if (!Path.IsPathRooted(path) && !string.IsNullOrEmpty(pdfReportPath))
                        {
                            var reportDir = Path.GetDirectoryName(pdfReportPath);
                            path = Path.Combine(reportDir ?? "", path);
                        }
                        if (File.Exists(path))
                        {
                            bmp = new Bitmap(path);
                        }
                    }
                }
                catch { }
            }
            _stepViewModels.Add(new PlaybackStepViewModel(step, bmp));
        }

        listSteps.ItemsSource = _stepViewModels;

        // 3. Update overall summary stats
        int total = steps.Count;
        int passed = steps.Count(s => s.Status.Equals("Passed", StringComparison.OrdinalIgnoreCase));
        int failed = steps.Count(s => s.Status.Equals("Failed", StringComparison.OrdinalIgnoreCase));
        int successRate = total > 0 ? (passed * 100 / total) : 100;

        txtTotalSteps.Text = total.ToString();
        txtPassedSteps.Text = passed.ToString();
        txtFailedSteps.Text = failed.ToString();
        txtSuccessRate.Text = $"{successRate}%";

        // 4. Configure seek timeline
        if (_frames.Count > 0)
        {
            _totalDurationMs = _frames.Last().TimestampMs;
            sliderSeek.Maximum = _frames.Count - 1;
            txtDuration.Text = $"{(_totalDurationMs / 1000.0):F2}s";
            
            // Show first frame
            ShowFrame(0);
        }
        else
        {
            _totalDurationMs = 0;
            sliderSeek.Maximum = 0;
            txtDuration.Text = "0.00s";
        }

        UpdateStatusText();

        // 5. Select first step and show its details
        if (_stepViewModels.Count > 0)
        {
            listSteps.SelectedItem = _stepViewModels[0];
            UpdateSelectedStepDetails(_stepViewModels[0]);
        }
        else
        {
            UpdateSelectedStepDetails(null);
        }
    }

    private void BtnPlay_Click(object? sender, EventArgs e)
    {
        if (_frames.Count == 0) return;

        if (_isPlaying)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    private void BtnStop_Click(object? sender, EventArgs e)
    {
        StopPlayback();
    }

    private void Play()
    {
        if (_frames.Count == 0) return;

        _isPlaying = true;
        btnPlay.Content = "Pause";
        
        if (_currentPositionMs >= _totalDurationMs)
        {
            _currentPositionMs = 0;
        }

        _stopwatch.Restart();
        _timer.Start();
    }

    private void Pause()
    {
        _isPlaying = false;
        btnPlay.Content = "Play";
        _stopwatch.Stop();
        _timer.Stop();
    }

    private void StopPlayback()
    {
        Pause();
        _currentPositionMs = 0;
        sliderSeek.Value = 0;
        ShowFrame(0);
        UpdateStatusText();

        // Clear highlight
        _isUpdatingSelectionFromPlayback = true;
        listSteps.SelectedItem = null;
        _isUpdatingSelectionFromPlayback = false;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_isPlaying || _frames.Count == 0) return;

        double elapsed = _stopwatch.Elapsed.TotalMilliseconds * _playbackSpeed;
        _stopwatch.Restart();

        _currentPositionMs += elapsed;

        if (_currentPositionMs >= _totalDurationMs)
        {
            _currentPositionMs = _totalDurationMs;
            StopPlayback();
            return;
        }

        // Find correct frame based on position
        int frameIndex = FindFrameIndexAtTime(_currentPositionMs);
        
        _isSeeking = true;
        sliderSeek.Value = frameIndex;
        _isSeeking = false;

        ShowFrame(frameIndex);
        UpdateStatusText();
        SyncStepHighlight();
    }

    private int FindFrameIndexAtTime(double timeMs)
    {
        if (_frames.Count <= 1) return 0;
        if (timeMs >= _frames.Last().TimestampMs) return _frames.Count - 1;

        // Binary search for frame
        int low = 0;
        int high = _frames.Count - 1;
        while (low <= high)
        {
            int mid = (low + high) / 2;
            double midTime = _frames[mid].TimestampMs;
            
            if (midTime == timeMs) return mid;
            if (midTime < timeMs) low = mid + 1;
            else high = mid - 1;
        }

        // Return closest frame before or at timeMs
        return Math.Max(0, low - 1);
    }

    private void ShowFrame(int index)
    {
        if (index < 0 || index >= _frames.Count) return;

        try
        {
            Bitmap bmp;
            if (_bitmapCache.TryGetValue(index, out var cachedBmp))
            {
                bmp = cachedBmp;
                // Move key to the end of insertion order to keep it hot (LRU)
                _cacheKeysOrder.Remove(index);
                _cacheKeysOrder.Add(index);
            }
            else
            {
                using var ms = new MemoryStream(_frames[index].Data);
                bmp = new Bitmap(ms);
                _bitmapCache[index] = bmp;
                _cacheKeysOrder.Add(index);

                // Keep cache size bounded (e.g. max 50 frame bitmaps)
                if (_cacheKeysOrder.Count > 50)
                {
                    int oldestIndex = _cacheKeysOrder[0];
                    _cacheKeysOrder.RemoveAt(0);
                    if (_bitmapCache.TryGetValue(oldestIndex, out var oldestBmp))
                    {
                        oldestBmp.Dispose();
                        _bitmapCache.Remove(oldestIndex);
                    }
                }
            }

            imgFrame.Source = bmp;
            txtMetadata.Text = $"Frame {index + 1}/{_frames.Count} @ {(int)_frames[index].TimestampMs}ms";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to render frame {index}: {ex.Message}");
        }
    }

    private void SliderSeek_PropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Slider.ValueProperty && !_isSeeking && _frames.Count > 0)
        {
            int index = (int)sliderSeek.Value;
            if (index >= 0 && index < _frames.Count)
            {
                _currentPositionMs = _frames[index].TimestampMs;
                ShowFrame(index);
                UpdateStatusText();
                SyncStepHighlight();
            }
        }
    }

    private void ListSteps_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSelectionFromPlayback || _frames.Count == 0) return;

        if (listSteps.SelectedItem is PlaybackStepViewModel stepVm)
        {
            SeekToTime(stepVm.RelativeStartMs);
            UpdateSelectedStepDetails(stepVm);
        }
    }

    private void SeekToTime(double timeMs)
    {
        _currentPositionMs = Math.Max(0, Math.Min(timeMs, _totalDurationMs));
        int frameIndex = FindFrameIndexAtTime(_currentPositionMs);
        
        _isSeeking = true;
        sliderSeek.Value = frameIndex;
        _isSeeking = false;
 
        ShowFrame(frameIndex);
        UpdateStatusText();
    }

    private void SyncStepHighlight()
    {
        if (_stepViewModels.Count == 0) return;

        var currentStep = _stepViewModels
            .OrderBy(s => s.RelativeStartMs)
            .LastOrDefault(s => s.RelativeStartMs <= _currentPositionMs);

        if (currentStep != null && listSteps.SelectedItem != currentStep)
        {
            _isUpdatingSelectionFromPlayback = true;
            listSteps.SelectedItem = currentStep;
            listSteps.ScrollIntoView(currentStep);
            _isUpdatingSelectionFromPlayback = false;

            UpdateSelectedStepDetails(currentStep);
        }
    }

    private void UpdateSelectedStepDetails(PlaybackStepViewModel? stepVm)
    {
        if (stepVm == null)
        {
            stepTelemetry.SelectedStep = null;
            _lastUpdatedStep = null;
            return;
        }

        if (_lastUpdatedStep == stepVm) return;
        _lastUpdatedStep = stepVm;

        stepTelemetry.SelectedStep = stepVm.Step;
    }


    private void SetSpeed(double speed)
    {
        _playbackSpeed = speed;
        UpdateSpeedButtons();
    }

    private void UpdateSpeedButtons()
    {
        btnSpeed1x.Classes.Remove("accent");
        btnSpeed2x.Classes.Remove("accent");
        btnSpeed4x.Classes.Remove("accent");

        if (_playbackSpeed == 1.0) btnSpeed1x.Classes.Add("accent");
        else if (_playbackSpeed == 2.0) btnSpeed2x.Classes.Add("accent");
        else if (_playbackSpeed == 4.0) btnSpeed4x.Classes.Add("accent");
    }

    private void UpdateStatusText()
    {
        txtTime.Text = $"{FormatTime(_currentPositionMs)} / {FormatTime(_totalDurationMs)}";
    }

    private string FormatTime(double ms)
    {
        int totalSec = (int)(ms / 1000);
        int minutes = totalSec / 60;
        int seconds = totalSec % 60;
        int msFraction = (int)((ms % 1000) / 100);
        return $"{minutes:00}:{seconds:00}.{msFraction}";
    }

    private void VideoPlaybackWindow_Closed(object? sender, EventArgs e)
    {
        _timer.Stop();
        _stopwatch.Stop();
        foreach (var bmp in _bitmapCache.Values)
        {
            bmp.Dispose();
        }
        _bitmapCache.Clear();
        _cacheKeysOrder.Clear();
        
        foreach (var vm in _stepViewModels)
        {
            vm.Screenshot?.Dispose();
        }
        _stepViewModels.Clear();
    }
}
