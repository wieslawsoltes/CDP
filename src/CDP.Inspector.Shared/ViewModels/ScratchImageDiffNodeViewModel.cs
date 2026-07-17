#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Media.Imaging;
using CDP.Editor.Nodes.ViewModels;
using SkiaSharp;
using Chrome.DevTools.Protocol;
using Microsoft.Extensions.Logging;

namespace CdpInspectorApp.ViewModels;

public class ScratchImageDiffNodeViewModel : ScratchNodeViewModelBase
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger<ScratchImageDiffNodeViewModel>();
    private string? _leftNodeId;
    private string? _rightNodeId;
    private ScratchNodeViewModelBase? _leftNode;
    private ScratchNodeViewModelBase? _rightNode;

    public Exception? LastDiffException { get; private set; }
    private string _leftTitle = "Left Page";
    private string _rightTitle = "Right Page";
    private WriteableBitmap? _diffImage;
    private double _diffPercentage;

    public string? LeftNodeId
    {
        get => _leftNodeId;
        set => RaiseAndSetIfChanged(ref _leftNodeId, value);
    }

    public string? RightNodeId
    {
        get => _rightNodeId;
        set => RaiseAndSetIfChanged(ref _rightNodeId, value);
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

    public ScratchNodeViewModelBase? RightNode
    {
        get => _rightNode;
        set
        {
            if (RaiseAndSetIfChanged(ref _rightNode, value))
            {
                if (_rightNodeId != value?.Id)
                {
                    _rightNodeId = value?.Id;
                    OnPropertyChanged(nameof(RightNodeId));
                }
            }
        }
    }

    public string LeftTitle
    {
        get => _leftTitle;
        set => RaiseAndSetIfChanged(ref _leftTitle, value);
    }

    public string RightTitle
    {
        get => _rightTitle;
        set => RaiseAndSetIfChanged(ref _rightTitle, value);
    }

    public WriteableBitmap? DiffImage
    {
        get => _diffImage;
        set
        {
            var old = _diffImage;
            if (RaiseAndSetIfChanged(ref _diffImage, value))
            {
                old?.Dispose();
                OnPropertyChanged(nameof(OutputJson));
                OnPropertyChanged(nameof(OutputJsonNode));
            }
        }
    }

    public double DiffPercentage
    {
        get => _diffPercentage;
        set
        {
            if (RaiseAndSetIfChanged(ref _diffPercentage, value))
            {
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
                ["diffPercentage"] = DiffPercentage,
                ["width"] = DiffImage?.PixelSize.Width ?? 0,
                ["height"] = DiffImage?.PixelSize.Height ?? 0,
                ["leftNodeId"] = LeftNodeId,
                ["rightNodeId"] = RightNodeId
            };
            return obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public override JsonNode? OutputJsonNode => new JsonObject
    {
        ["diffPercentage"] = DiffPercentage,
        ["width"] = DiffImage?.PixelSize.Width ?? 0,
        ["height"] = DiffImage?.PixelSize.Height ?? 0,
        ["leftNodeId"] = LeftNodeId,
        ["rightNodeId"] = RightNodeId
    };

    public ScratchImageDiffNodeViewModel()
    {
        TitleBackground = Avalonia.Media.Brush.Parse("#b06000");
        BorderBrush = Avalonia.Media.Brush.Parse("#e08000");

        AddInputPin("left", "Left Image");
        AddInputPin("right", "Right Image");
        AddOutputPin("diff_img", "Diff Image");
    }

    public void UpdateDiff(Func<string, ScratchNodeViewModelBase?> getNodeById, IEnumerable<CDP.Editor.Nodes.ViewModels.ConnectionViewModel> connections)
    {
        var incoming = connections
            .Where(c => c.ToNode == this && c.FromNode is ScratchNodeViewModelBase)
            .ToList();

        string? resolvedLeftId = LeftNodeId;
        string? resolvedRightId = RightNodeId;

        // Pin-based lookup
        var leftConn = connections.FirstOrDefault(c => c.ToPin?.Owner == this && c.ToPin.Id == "left");
        var rightConn = connections.FirstOrDefault(c => c.ToPin?.Owner == this && c.ToPin.Id == "right");

        if (leftConn?.FromNode is ScratchNodeViewModelBase leftBase)
        {
            resolvedLeftId = leftBase.Id;
        }
        else if (string.IsNullOrEmpty(resolvedLeftId) && incoming.Count > 0)
        {
            resolvedLeftId = incoming[0].FromNode?.Id;
        }

        if (rightConn?.FromNode is ScratchNodeViewModelBase rightBase)
        {
            resolvedRightId = rightBase.Id;
        }
        else if (string.IsNullOrEmpty(resolvedRightId) && incoming.Count > 1)
        {
            resolvedRightId = incoming[1].FromNode?.Id;
        }

        var leftNode = !string.IsNullOrEmpty(resolvedLeftId) ? getNodeById(resolvedLeftId) : null;
        var rightNode = !string.IsNullOrEmpty(resolvedRightId) ? getNodeById(resolvedRightId) : null;

        // Cycle detection
        var visited = new HashSet<string> { this.Id };
        if (leftNode != null && DetectCycle(leftNode, visited))
        {
            leftNode = null;
        }
        if (rightNode != null && DetectCycle(rightNode, visited))
        {
            rightNode = null;
        }

        bool leftChanged = _leftNode != leftNode;
        bool rightChanged = _rightNode != rightNode;

        if (leftChanged)
        {
            _leftNode = leftNode;
            _leftNodeId = leftNode?.Id;
            OnPropertyChanged(nameof(LeftNode));
            OnPropertyChanged(nameof(LeftNodeId));
        }

        if (rightChanged)
        {
            _rightNode = rightNode;
            _rightNodeId = rightNode?.Id;
            OnPropertyChanged(nameof(RightNode));
            OnPropertyChanged(nameof(RightNodeId));
        }

        LeftTitle = leftNode != null ? $"{leftNode.Name}" : "Left (No Input)";
        RightTitle = rightNode != null ? $"{rightNode.Name}" : "Right (No Input)";

        ComputeDiffImage();
    }

    private void ComputeDiffImage()
    {
        try
        {
            LastDiffException = null;
            using var leftSk = GetNodeSkBitmap(LeftNode);
            using var rightSk = GetNodeSkBitmap(RightNode);

            if (leftSk == null || rightSk == null)
            {
                DiffImage = null;
                DiffPercentage = 0.0;
                return;
            }

            int width = Math.Max(leftSk.Width, rightSk.Width);
            int height = Math.Max(leftSk.Height, rightSk.Height);

            if (width <= 0 || height <= 0)
            {
                DiffImage = null;
                DiffPercentage = 0.0;
                return;
            }

            var diffBmp = new WriteableBitmap(
                new Avalonia.PixelSize(width, height),
                new Avalonia.Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888,
                Avalonia.Platform.AlphaFormat.Premul);

            int diffCount = 0;
            int totalPixels = width * height;

            using (var locked = diffBmp.Lock())
            {
                unsafe
                {
                    byte* dest = (byte*)locked.Address;
                    int destRowBytes = locked.RowBytes;

                    byte* leftPixels = (byte*)leftSk.GetPixels();
                    byte* rightPixels = (byte*)rightSk.GetPixels();

                    int leftRowBytes = leftSk.RowBytes;
                    int rightRowBytes = rightSk.RowBytes;
                    int leftBpp = leftSk.BytesPerPixel;
                    int rightBpp = rightSk.BytesPerPixel;

                    bool leftIsBgra = leftSk.ColorType == SKColorType.Bgra8888;
                    bool rightIsBgra = rightSk.ColorType == SKColorType.Bgra8888;

                    for (int y = 0; y < height; y++)
                    {
                        byte* destRow = dest + y * destRowBytes;
                        bool inLeftY = y < leftSk.Height;
                        bool inRightY = y < rightSk.Height;

                        byte* leftRow = inLeftY ? (leftPixels + y * leftRowBytes) : null;
                        byte* rightRow = inRightY ? (rightPixels + y * rightRowBytes) : null;

                        for (int x = 0; x < width; x++)
                        {
                            bool hasLeft = inLeftY && x < leftSk.Width;
                            bool hasRight = inRightY && x < rightSk.Width;

                            bool isDifferent = false;
                            byte lr = 0, lg = 0, lb = 0;
                            byte rr = 0, rg = 0, rb = 0;

                            if (hasLeft && leftRow != null)
                            {
                                byte* p = leftRow + x * leftBpp;
                                if (leftBpp == 4)
                                {
                                    if (leftIsBgra)
                                    {
                                        lb = p[0];
                                        lg = p[1];
                                        lr = p[2];
                                    }
                                    else
                                    {
                                        lr = p[0];
                                        lg = p[1];
                                        lb = p[2];
                                    }
                                }
                                else
                                {
                                    var c = leftSk.GetPixel(x, y);
                                    lr = c.Red;
                                    lg = c.Green;
                                    lb = c.Blue;
                                }
                            }

                            if (hasRight && rightRow != null)
                            {
                                byte* p = rightRow + x * rightBpp;
                                if (rightBpp == 4)
                                {
                                    if (rightIsBgra)
                                    {
                                        rb = p[0];
                                        rg = p[1];
                                        rr = p[2];
                                    }
                                    else
                                    {
                                        rr = p[0];
                                        rg = p[1];
                                        rb = p[2];
                                    }
                                }
                                else
                                {
                                    var c = rightSk.GetPixel(x, y);
                                    rr = c.Red;
                                    rg = c.Green;
                                    rb = c.Blue;
                                }
                            }

                            if (hasLeft != hasRight)
                            {
                                isDifferent = true;
                            }
                            else if (hasLeft && hasRight)
                            {
                                if (lr != rr || lg != rg || lb != rb)
                                {
                                    isDifferent = true;
                                }
                            }

                            int offset = x * 4;
                            if (isDifferent)
                            {
                                diffCount++;
                                destRow[offset + 0] = 0;   // B
                                destRow[offset + 1] = 0;   // G
                                destRow[offset + 2] = 255; // R
                                destRow[offset + 3] = 255; // A
                            }
                            else
                            {
                                destRow[offset + 0] = lb;
                                destRow[offset + 1] = lg;
                                destRow[offset + 2] = lr;
                                destRow[offset + 3] = 255;
                            }
                        }
                    }
                }
            }

            DiffImage = diffBmp;
            DiffPercentage = totalPixels > 0 ? ((double)diffCount / totalPixels) * 100.0 : 0.0;
        }
        catch (Exception ex)
        {
            LastDiffException = ex;
            Logger.LogErrorMessage("ScratchImageDiffNode", "Image diff computation failed", ex);
            DiffImage = null;
            DiffPercentage = 0.0;
        }
    }

    private SKBitmap? GetNodeSkBitmap(ScratchNodeViewModelBase? node)
    {
        if (node == null) return null;

        if (node is ScratchPageNodeViewModel pageNode)
        {
            if (string.IsNullOrEmpty(pageNode.ScreenshotBase64))
            {
                return null;
            }
            try
            {
                byte[] bytes = Convert.FromBase64String(pageNode.ScreenshotBase64);
                return SKBitmap.Decode(bytes);
            }
            catch
            {
                return null;
            }
        }

        if (node is ScratchImageDiffNodeViewModel diffNode)
        {
            var writeable = diffNode.DiffImage;
            if (writeable == null) return null;

            try
            {
                var info = new SKImageInfo(writeable.PixelSize.Width, writeable.PixelSize.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                var skBmp = new SKBitmap(info);
                using (var locked = writeable.Lock())
                {
                    unsafe
                    {
                        Buffer.MemoryCopy(
                            (void*)locked.Address,
                            (void*)skBmp.GetPixels(),
                            info.BytesSize,
                            info.BytesSize);
                    }
                }
                return skBmp;
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private bool DetectCycle(ScratchNodeViewModelBase? node, HashSet<string> visitedIds)
    {
        if (node == null) return false;
        if (!visitedIds.Add(node.Id)) return true;

        if (node is ScratchImageDiffNodeViewModel diffNode)
        {
            if (DetectCycle(diffNode.LeftNode, visitedIds)) return true;
            if (DetectCycle(diffNode.RightNode, visitedIds)) return true;
        }
        else if (node is ScratchDiffNodeViewModel textDiffNode)
        {
            if (DetectCycle(textDiffNode.LeftNode, visitedIds)) return true;
            if (DetectCycle(textDiffNode.RightNode, visitedIds)) return true;
        }
        else if (node is ScratchAssertionNodeViewModel assertNode)
        {
            if (DetectCycle(assertNode.InputNode, visitedIds)) return true;
        }

        visitedIds.Remove(node.Id);
        return false;
    }

    public override void Dispose()
    {
        DiffImage?.Dispose();
        base.Dispose();
    }
}
