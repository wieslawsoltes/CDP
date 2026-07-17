#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using CDP.Editor.Nodes.ViewModels;
using CdpInspectorApp.Services;
using Chrome.DevTools.Protocol;
using Microsoft.Extensions.Logging;

namespace CdpInspectorApp.ViewModels;

public class ScratchPageNodeViewModel : ScratchNodeViewModelBase
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger<ScratchPageNodeViewModel>();
    private readonly ICdpService? _cdpService;
    private Bitmap? _screenshotImage;
    private string? _screenshotBase64;
    private bool _isCapturing;
    private bool _isSyncedWithTimeMachine;
    private int _pinnedFrameIndex = -1;
    private bool _isDisposed;
    private string? _leftNodeId;
    private ScratchNodeViewModelBase? _leftNode;

    public Exception? LastDecodeException { get; private set; }

    public string? LeftNodeId
    {
        get => _leftNodeId;
        set => RaiseAndSetIfChanged(ref _leftNodeId, value);
    }

    public ScratchNodeViewModelBase? LeftNode
    {
        get => _leftNode;
        set
        {
            if (RaiseAndSetIfChanged(ref _leftNode, value))
            {
                if (_leftNodeId != value?.Id)
                {
                    _leftNodeId = value?.Id;
                    OnPropertyChanged(nameof(LeftNodeId));
                }
            }
        }
    }

    public ITimeMachineService? TimeMachine => _cdpService?.TimeMachine;

    public Bitmap? ScreenshotImage
    {
        get => _screenshotImage;
        set
        {
            var old = _screenshotImage;
            if (RaiseAndSetIfChanged(ref _screenshotImage, value))
            {
                old?.Dispose();
                OnPropertyChanged(nameof(OutputJson));
                OnPropertyChanged(nameof(OutputJsonNode));
            }
        }
    }

    public string? ScreenshotBase64
    {
        get => _screenshotBase64;
        set
        {
            if (RaiseAndSetIfChanged(ref _screenshotBase64, value))
            {
                UpdateBitmapFromBase64();
                OnPropertyChanged(nameof(OutputJson));
                OnPropertyChanged(nameof(OutputJsonNode));
            }
        }
    }

    public bool IsCapturing
    {
        get => _isCapturing;
        set => RaiseAndSetIfChanged(ref _isCapturing, value);
    }

    public bool IsSyncedWithTimeMachine
    {
        get => _isSyncedWithTimeMachine;
        set
        {
            if (RaiseAndSetIfChanged(ref _isSyncedWithTimeMachine, value))
            {
                UpdateSubscriptions();
                OnPropertyChanged(nameof(OutputJson));
                OnPropertyChanged(nameof(OutputJsonNode));
            }
        }
    }

    public int PinnedFrameIndex
    {
        get => _pinnedFrameIndex;
        set
        {
            if (RaiseAndSetIfChanged(ref _pinnedFrameIndex, value))
            {
                if (IsSyncedWithTimeMachine)
                {
                    SyncScreenshotWithTimeMachine();
                }
                OnPropertyChanged(nameof(OutputJson));
                OnPropertyChanged(nameof(OutputJsonNode));
            }
        }
    }

    public override string OutputJson
    {
        get
        {
            var obj = new JsonObject
            {
                ["screenshotBase64"] = ScreenshotBase64,
                ["width"] = ScreenshotImage?.Size.Width ?? 0,
                ["height"] = ScreenshotImage?.Size.Height ?? 0,
                ["isSyncedWithTimeMachine"] = IsSyncedWithTimeMachine,
                ["pinnedFrameIndex"] = PinnedFrameIndex
            };
            return obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public override JsonNode? OutputJsonNode => new JsonObject
    {
        ["screenshotBase64"] = ScreenshotBase64,
        ["width"] = ScreenshotImage?.Size.Width ?? 0,
        ["height"] = ScreenshotImage?.Size.Height ?? 0,
        ["isSyncedWithTimeMachine"] = IsSyncedWithTimeMachine,
        ["pinnedFrameIndex"] = PinnedFrameIndex
    };

    public ICommand CaptureCommand { get; }

    public ScratchPageNodeViewModel() : this(null)
    {
    }

    public ScratchPageNodeViewModel(ICdpService? cdpService)
    {
        _cdpService = cdpService;
        TitleBackground = Avalonia.Media.Brush.Parse("#9c27b0");
        BorderBrush = Avalonia.Media.Brush.Parse("#ba68c8");

        AddOutputPin("screenshot", "Screenshot");

        CaptureCommand = new RelayCommand(async () => await CaptureScreenshotAsync());
        UpdateSubscriptions();
    }

    private async Task CaptureScreenshotAsync()
    {
        if (_cdpService == null || !_cdpService.IsConnected)
        {
            return;
        }

        IsCapturing = true;
        try
        {
            var res = await _cdpService.SendCommandAsync("Page.captureScreenshot", new JsonObject());
            string base64 = res?["data"]?.GetValue<string>() ?? "";
            if (!string.IsNullOrEmpty(base64))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    ScreenshotBase64 = base64;
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("ScratchPageNode", "Screenshot capture failed in node", ex);
        }
        finally
        {
            IsCapturing = false;
        }
    }

    private void UpdateBitmapFromBase64()
    {
        if (string.IsNullOrEmpty(ScreenshotBase64))
        {
            ScreenshotImage = null;
            return;
        }

        try
        {
            LastDecodeException = null;
            byte[] bytes = Convert.FromBase64String(ScreenshotBase64);
            using var ms = new MemoryStream(bytes);
            var bitmap = new Bitmap(ms);
            ScreenshotImage = bitmap;
        }
        catch (Exception ex)
        {
            LastDecodeException = ex;
            Logger.LogErrorMessage("ScratchPageNode", "Bitmap decode error", ex);
            ScreenshotImage = null;
        }
    }

    private void UpdateSubscriptions()
    {
        if (TimeMachine == null) return;

        TimeMachine.FrameChanged -= TimeMachine_FrameChanged;
        TimeMachine.PropertyChanged -= TimeMachine_PropertyChanged;

        if (IsSyncedWithTimeMachine)
        {
            TimeMachine.FrameChanged += TimeMachine_FrameChanged;
            TimeMachine.PropertyChanged += TimeMachine_PropertyChanged;
            SyncScreenshotWithTimeMachine();
        }
    }

    private void TimeMachine_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ITimeMachineService.CurrentFrameIndex))
        {
            if (IsSyncedWithTimeMachine && PinnedFrameIndex < 0)
            {
                SyncScreenshotWithTimeMachine();
            }
        }
    }

    private void TimeMachine_FrameChanged(object? sender, EventArgs e)
    {
        if (IsSyncedWithTimeMachine && PinnedFrameIndex < 0)
        {
            SyncScreenshotWithTimeMachine();
        }
    }

    private void SyncScreenshotWithTimeMachine()
    {
        if (TimeMachine == null || !IsSyncedWithTimeMachine) return;

        var idx = PinnedFrameIndex >= 0 ? PinnedFrameIndex : TimeMachine.CurrentFrameIndex;
        var frames = TimeMachine.Frames;
        if (idx >= 0 && idx < frames.Count)
        {
            var frame = frames[idx];
            if (frame.Domain == "Page" || frame.Method == "Page.screencastFrame")
            {
                string? base64 = null;
                if (frame.Params != null && frame.Params.ContainsKey("data"))
                {
                    base64 = frame.Params["data"]?.GetValue<string>();
                }
                else if (frame.Payload != null && frame.Payload.ContainsKey("data"))
                {
                    base64 = frame.Payload["data"]?.GetValue<string>();
                }

                if (!string.IsNullOrEmpty(base64))
                {
                    try
                    {
                        byte[] bytes = Convert.FromBase64String(base64);
                        using var ms = new MemoryStream(bytes);
                        var bitmap = new Bitmap(ms);
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            ScreenshotBase64 = base64;
                            ScreenshotImage = bitmap;
                        });
                    }
                    catch
                    {
                        // Ignore decode failures
                    }
                }
                else
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        ScreenshotBase64 = null;
                        ScreenshotImage = null;
                    });
                }
            }
        }
    }

    public override void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (TimeMachine != null)
        {
            TimeMachine.FrameChanged -= TimeMachine_FrameChanged;
            TimeMachine.PropertyChanged -= TimeMachine_PropertyChanged;
        }

        ScreenshotImage?.Dispose();
        base.Dispose();
    }
}
