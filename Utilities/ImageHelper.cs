using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace GoodGovernanceApp.Utilities;

public static class ImageHelper
{
    public const string OpenImageFileDialogFilter = 
        "All Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp;*.ico;*.tiff;*.jfif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp;*.ico;*.tiff;*.jfif|PNG Images (*.png)|*.png|JPEG Images (*.jpg;*.jpeg;*.jfif)|*.jpg;*.jpeg;*.jfif|Bitmap Images (*.bmp)|*.bmp|WebP Images (*.webp)|*.webp|All Files (*.*)|*.*";

    /// <summary>
    /// Safely loads a BitmapImage from an absolute file path, relative file path, or pack URI without throwing exceptions.
    /// Uses BitmapCacheOption.OnLoad so file locks are never held.
    /// </summary>
    public static BitmapImage? LoadBitmapSafe(string? pathOrUri, string? packUriFallback = null)
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

    /// <summary>
    /// Resolves relative or absolute path against common application directories.
    /// </summary>
    public static string? ResolveFilePath(string rawPath)
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
