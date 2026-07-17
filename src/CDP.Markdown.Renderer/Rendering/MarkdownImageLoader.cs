namespace CDP.Markdown.Renderer.Rendering;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using SkiaSharp;

public static class MarkdownImageLoader
{
    private static readonly ConcurrentDictionary<string, SKBitmap> _imageCache = new();
    private static readonly ConcurrentDictionary<string, List<Action>> _pendingCallbacks = new();

    public static SKBitmap? GetOrLoadImage(string? urlOrPath, Action? onLoaded)
    {
        if (string.IsNullOrEmpty(urlOrPath))
        {
            return null;
        }

        if (_imageCache.TryGetValue(urlOrPath, out var cachedBitmap))
        {
            return cachedBitmap;
        }

        if (onLoaded != null)
        {
            var callbacks = _pendingCallbacks.GetOrAdd(urlOrPath, _ => new List<Action>());
            lock (callbacks)
            {
                callbacks.Add(onLoaded);
                if (callbacks.Count > 1)
                {
                    // Already loading
                    return null;
                }
            }
        }
        else
        {
            if (_pendingCallbacks.ContainsKey(urlOrPath))
            {
                // Already loading
                return null;
            }
            _pendingCallbacks.TryAdd(urlOrPath, new List<Action>());
        }

        // Start async loading
        Task.Run(async () =>
        {
            try
            {
                byte[]? bytes = null;
                string cacheDir = Path.Combine(Path.GetTempPath(), "CdpImageCache");
                Directory.CreateDirectory(cacheDir);

                string hash;
                using (var md5 = MD5.Create())
                {
                    byte[] inputBytes = Encoding.UTF8.GetBytes(urlOrPath);
                    byte[] hashBytes = md5.ComputeHash(inputBytes);
                    hash = Convert.ToHexString(hashBytes);
                }

                string ext = Path.GetExtension(urlOrPath);
                if (string.IsNullOrEmpty(ext))
                {
                    ext = ".png";
                }
                string cachedFilePath = Path.Combine(cacheDir, hash + ext);

                if (File.Exists(cachedFilePath))
                {
                    bytes = await File.ReadAllBytesAsync(cachedFilePath);
                }
                else
                {
                    if (urlOrPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        urlOrPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var client = new HttpClient())
                        {
                            bytes = await client.GetByteArrayAsync(urlOrPath);
                        }
                        if (bytes != null)
                        {
                            await File.WriteAllBytesAsync(cachedFilePath, bytes);
                        }
                    }
                    else
                    {
                        if (File.Exists(urlOrPath))
                        {
                            bytes = await File.ReadAllBytesAsync(urlOrPath);
                            await File.WriteAllBytesAsync(cachedFilePath, bytes);
                        }
                    }
                }

                if (bytes != null)
                {
                    using (var ms = new MemoryStream(bytes))
                    {
                        var bitmap = SKBitmap.Decode(ms);
                        if (bitmap != null)
                        {
                            _imageCache[urlOrPath] = bitmap;
                        }
                    }
                }
            }
            catch
            {
                // Fallback gracefully on network / file errors
            }
            finally
            {
                if (_pendingCallbacks.TryRemove(urlOrPath, out var callbacks))
                {
                    lock (callbacks)
                    {
                        foreach (var cb in callbacks)
                        {
                            try
                            {
                                cb();
                            }
                            catch
                            {
                                // Suppress callback execution exceptions
                            }
                        }
                    }
                }
            }
        });

        return null;
    }

    public static void ClearCache()
    {
        foreach (var bmp in _imageCache.Values)
        {
            bmp.Dispose();
        }
        _imageCache.Clear();
        _pendingCallbacks.Clear();
    }
}
