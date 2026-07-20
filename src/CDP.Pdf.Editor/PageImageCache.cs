using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using SkiaSharp;
using CDP.Pdf.Editor.Model;

namespace CDP.Pdf.Editor;

public class PageImageCache : IDisposable
{
    private readonly PdfDocumentModel _documentModel;
    private readonly ConcurrentDictionary<(int pageNum, float scale), SKBitmap> _cache = new();
    private readonly List<(int pageNum, float scale)> _usageOrder = new();
    private readonly object _lock = new();
    private readonly int _maxSize;

    public int CacheSize => _cache.Count;

    public PageImageCache(PdfDocumentModel documentModel, int maxSize = 8)
    {
        _documentModel = documentModel;
        _maxSize = maxSize;
    }

    public SKBitmap? GetPageBitmap(int pageNumber, float scale)
    {
        var key = (pageNumber, scale);

        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var bitmap))
            {
                // Move to end (most recently used)
                _usageOrder.Remove(key);
                _usageOrder.Add(key);
                return bitmap;
            }
        }

        // Render bitmap synchronously (intended to be called from Task.Run background thread)
        var newBitmap = _documentModel.RenderPageToBitmap(pageNumber, scale);
        if (newBitmap != null)
        {
            lock (_lock)
            {
                if (_cache.TryAdd(key, newBitmap))
                {
                    _usageOrder.Add(key);
                    EvictIfNecessary();
                }
                else
                {
                    newBitmap.Dispose();
                }
            }
            return _cache.GetValueOrDefault(key);
        }
        return null;
    }

    public SKBitmap? GetBestAvailablePageBitmap(int pageNumber, out float actualScale)
    {
        actualScale = 0f;
        lock (_lock)
        {
            SKBitmap? bestBitmap = null;
            float bestDiff = float.MaxValue;
            
            foreach (var kvp in _cache)
            {
                if (kvp.Key.pageNum == pageNumber)
                {
                    float diff = Math.Abs(kvp.Key.scale - actualScale);
                    if (bestBitmap == null || diff < bestDiff)
                    {
                        bestBitmap = kvp.Value;
                        bestDiff = diff;
                        actualScale = kvp.Key.scale;
                    }
                }
            }
            return bestBitmap;
        }
    }

    private void EvictIfNecessary()
    {
        while (_usageOrder.Count > _maxSize)
        {
            var oldestKey = _usageOrder[0];
            _usageOrder.RemoveAt(0);
            if (_cache.TryRemove(oldestKey, out var bitmap))
            {
                bitmap.Dispose();
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            foreach (var bitmap in _cache.Values)
            {
                bitmap.Dispose();
            }
            _cache.Clear();
            _usageOrder.Clear();
        }
    }

    public void Dispose()
    {
        Clear();
    }
}
