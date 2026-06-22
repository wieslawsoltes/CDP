using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CdpInspectorApp.Services;

namespace CdpInspectorApp.Views;

public class PlaybackFrame
{
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public double TimestampMs { get; set; }
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

    public PlaybackStepViewModel(StepReportItem item, Bitmap? screenshot)
    {
        Index = item.Index;
        Action = item.Action;
        ActionDisplay = item.ActionDisplay;
        StatusText = item.Status.ToUpper();
        Selector = item.Selector;
        Value = item.Value;
        DurationMs = item.DurationMs;
        RelativeStartMs = item.RelativeStartMs;
        Screenshot = screenshot;
    }
}

public partial class VideoPlaybackWindow : Window
{
    private List<PlaybackFrame> _frames = new();
    private readonly Dictionary<int, Bitmap> _bitmapCache = new();
    private readonly List<PlaybackStepViewModel> _stepViewModels = new();
    
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = new();
    
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
        _frames = frames ?? new List<PlaybackFrame>();
        
        // 1. Clear old cache
        foreach (var bmp in _bitmapCache.Values)
        {
            bmp.Dispose();
        }
        _bitmapCache.Clear();

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
        btnPlay.Content = "⏸ Pause";
        
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
        btnPlay.Content = "▶ Play";
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
            if (!_bitmapCache.TryGetValue(index, out var bmp))
            {
                using var ms = new MemoryStream(_frames[index].Data);
                bmp = new Bitmap(ms);
                _bitmapCache[index] = bmp;
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
        }
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
        
        foreach (var vm in _stepViewModels)
        {
            vm.Screenshot?.Dispose();
        }
        _stepViewModels.Clear();
    }
}
