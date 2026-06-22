using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace CdpInspectorApp.Views;

public class PlaybackFrame
{
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public double TimestampMs { get; set; }
}

public partial class VideoPlaybackWindow : Window
{
    private List<PlaybackFrame> _frames = new();
    private readonly Dictionary<int, Bitmap> _bitmapCache = new();
    
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = new();
    
    private bool _isPlaying = false;
    private double _currentPositionMs = 0;
    private double _playbackSpeed = 1.0;
    private bool _isSeeking = false;
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
        
        Closed += VideoPlaybackWindow_Closed;
        
        UpdateSpeedButtons();
    }

    public void SetFrames(List<PlaybackFrame> frames)
    {
        _frames = frames ?? new List<PlaybackFrame>();
        
        // Clear old cache
        foreach (var bmp in _bitmapCache.Values)
        {
            bmp.Dispose();
        }
        _bitmapCache.Clear();

        if (_frames.Count > 0)
        {
            _totalDurationMs = _frames.Last().TimestampMs;
            sliderSeek.Maximum = _frames.Count - 1;
            
            // Show first frame
            ShowFrame(0);
        }
        else
        {
            _totalDurationMs = 0;
            sliderSeek.Maximum = 0;
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
            }
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
    }
}
