using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GoodGovernanceApp.Utilities;

public static class ImageHelper
{
    public const string OpenImageFileDialogFilter = 
        "All Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp;*.ico;*.tiff;*.jfif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp;*.ico;*.tiff;*.jfif|PNG Images (*.png)|*.png|JPEG Images (*.jpg;*.jpeg;*.jfif)|*.jpg;*.jpeg;*.jfif|Bitmap Images (*.bmp)|*.bmp|WebP Images (*.webp)|*.webp|All Files (*.*)|*.*";

    // Cache resolved paths so each image costs 1 lookup instead of ~20 File.Exists probes.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string?> _pathCache = new(StringComparer.OrdinalIgnoreCase);
    private const int ThumbnailWidth = 256;

    /// <summary>
    /// Loads a LOGO image with its baked-in white background stripped to transparent.
    /// Use for seals / brand logos only — never for person photos (a white shirt near
    /// the edge would get punched transparent). Returns a frozen ImageSource, or null.
    /// </summary>
    public static ImageSource? LoadLogoSafe(string? pathOrUri, string? packUriFallback = null, int decodePixelWidth = ThumbnailWidth, byte whiteThreshold = 242)
    {
        BitmapImage? bmp = LoadBitmapSafe(pathOrUri, packUriFallback, decodePixelWidth);
        if (bmp == null) return null;
        try
        {
            return RemoveWhiteBackground(bmp, whiteThreshold);
        }
        catch
        {
            return bmp;
        }
    }

    /// <summary>
    /// Flood-fills from every border pixel through near-white pixels and clears their
    /// alpha. Only background white connected to the edges is removed — white areas
    /// sealed inside the artwork (e.g. inside a seal) are preserved.
    /// </summary>
    private static ImageSource RemoveWhiteBackground(BitmapSource source, byte threshold)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int w = converted.PixelWidth;
        int h = converted.PixelHeight;
        if (w <= 0 || h <= 0) return source;
        int stride = w * 4;
        byte[] pixels = new byte[h * stride];
        converted.CopyPixels(pixels, stride, 0);

        bool IsWhite(int idx) =>
            pixels[idx + 2] >= threshold &&  // R
            pixels[idx + 1] >= threshold &&  // G
            pixels[idx] >= threshold;        // B

        var visited = new bool[w * h];
        var queue = new Queue<int>(w * 2 + h * 2);

        void EnqueueBorder(int x, int y)
        {
            int p = y * w + x;
            if (visited[p]) return;
            visited[p] = true;
            if (IsWhite(p * 4)) queue.Enqueue(p);
        }

        for (int x = 0; x < w; x++)
        {
            EnqueueBorder(x, 0);
            EnqueueBorder(x, h - 1);
        }
        for (int y = 0; y < h; y++)
        {
            EnqueueBorder(0, y);
            EnqueueBorder(w - 1, y);
        }

        int cleared = 0;
        var clearedMask = new bool[w * h];
        while (queue.Count > 0)
        {
            int p = queue.Dequeue();
            int px = p % w;
            int py = p / w;

            pixels[p * 4 + 3] = 0; // alpha → transparent
            clearedMask[p] = true;
            cleared++;

            if (px > 0) TryVisit(p - 1);
            if (px < w - 1) TryVisit(p + 1);
            if (py > 0) TryVisit(p - w);
            if (py < h - 1) TryVisit(p + w);
        }

        // Safety: if almost nothing (or everything) was edge-white, the image has
        // no real white backdrop — return the original untouched.
        if (cleared < 8 || cleared == w * h) return source;

        // Defringe: JPEG halos leave a 1px grayish rim just inside the cleared
        // area. Clear rim pixels that are still light gray (all channels >= 200).
        // Saturated artwork (e.g. the seal's yellow ring, low blue) is untouched.
        const byte fringeThreshold = 200;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int p = y * w + x;
                if (clearedMask[p]) continue;
                bool touchesCleared =
                    (x > 0 && clearedMask[p - 1]) ||
                    (x < w - 1 && clearedMask[p + 1]) ||
                    (y > 0 && clearedMask[p - w]) ||
                    (y < h - 1 && clearedMask[p + w]);
                if (!touchesCleared) continue;
                int i = p * 4;
                if (pixels[i + 2] >= fringeThreshold &&
                    pixels[i + 1] >= fringeThreshold &&
                    pixels[i] >= fringeThreshold)
                {
                    pixels[i + 3] = 0;
                }
            }
        }

        void TryVisit(int n)
        {
            if (visited[n]) return;
            visited[n] = true;
            if (IsWhite(n * 4)) queue.Enqueue(n);
        }

        var result = new WriteableBitmap(w, h, converted.DpiX, converted.DpiY, PixelFormats.Bgra32, null);
        result.WritePixels(new System.Windows.Int32Rect(0, 0, w, h), pixels, stride, 0);
        result.Freeze();
        return result;
    }

    /// <summary>
    /// Safely loads a BitmapImage from an absolute file path, relative file path, or pack URI without throwing exceptions.
    /// Uses BitmapCacheOption.OnLoad so file locks are never held.
    /// Decodes to a thumbnail width so full-resolution photos don't stall the UI or blow memory.
    /// </summary>
    public static BitmapImage? LoadBitmapSafe(string? pathOrUri, string? packUriFallback = null, int decodePixelWidth = ThumbnailWidth)
    {
        if (!string.IsNullOrWhiteSpace(pathOrUri))
        {
            // 1. Check if it's already a pack URI
            if (pathOrUri.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.DecodePixelWidth = decodePixelWidth;
                    bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bi.UriSource = new Uri(pathOrUri, UriKind.Absolute);
                    bi.EndInit();
                    bi.Freeze();
                    return bi;
                }
                catch { }
            }

            // 2. Resolve file path from various potential directories
            string? resolvedPath = ResolveFilePath(pathOrUri);
            if (!string.IsNullOrEmpty(resolvedPath) && File.Exists(resolvedPath))
            {
                try
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.DecodePixelWidth = decodePixelWidth;
                    bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bi.UriSource = new Uri(resolvedPath, UriKind.Absolute);
                    bi.EndInit();
                    bi.Freeze();
                    return bi;
                }
                catch
                {
                    // In case of non-standard formats, try reading bytes directly
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(resolvedPath);
                        using var ms = new MemoryStream(bytes);
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.DecodePixelWidth = decodePixelWidth;
                        bi.StreamSource = ms;
                        bi.EndInit();
                        bi.Freeze();
                        return bi;
                    }
                    catch { }
                }
            }
        }

        // 3. Fallback to pack URI if provided
        if (!string.IsNullOrWhiteSpace(packUriFallback))
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(packUriFallback, UriKind.Absolute);
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch { }
        }

        return null;
    }

    public static string? ResolveFilePath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath)) return null;

        if (_pathCache.TryGetValue(rawPath, out var cached))
            return cached;

        string? result = ResolveFilePathCore(rawPath);
        _pathCache[rawPath] = result;
        return result;
    }

    /// <summary>
    /// Resolves relative or absolute path against common application directories.
    /// </summary>
    private static string? ResolveFilePathCore(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath)) return null;

        if (Path.IsPathRooted(rawPath) && File.Exists(rawPath))
            return rawPath;

        var searchRoots = new[]
        {
            AppDomain.CurrentDomain.BaseDirectory,
            Path.GetDirectoryName(Environment.ProcessPath) ?? "",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GoodGovernanceApp"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GoodGovernanceApp", "Logos"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GoodGovernanceApp", "ProfilePhotos"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GoodGovernanceApp", "Uploads"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ProfilePhotos"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Uploads")
        };

        foreach (var root in searchRoots)
        {
            if (string.IsNullOrWhiteSpace(root)) continue;

            string candidate = Path.Combine(root, rawPath);
            if (File.Exists(candidate))
                return candidate;

            string filenameOnly = Path.GetFileName(rawPath);
            if (!string.IsNullOrEmpty(filenameOnly))
            {
                candidate = Path.Combine(root, filenameOnly);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }
}
