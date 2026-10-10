// <copyright file="IconHelper.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Velopack.UI.Helpers;

/// <summary>
/// Reads Windows file and folder icons.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
internal static class IconHelper
{
    private static readonly Dictionary<string, ImageSource> LargeIconCache = [];
    private static readonly Dictionary<string, ImageSource> SmallIconCache = [];

    /// <summary>
    /// Specifies whether the folder is open.
    /// </summary>
    public enum FolderType
    {
        /// <summary>Closed folder.</summary>
        Closed,

        /// <summary>Open folder.</summary>
        Open,
    }

    /// <summary>
    /// Specifies the icon size.
    /// </summary>
    public enum IconSize
    {
        /// <summary>Large icon.</summary>
        Large,

        /// <summary>Small icon.</summary>
        Small,
    }

    /// <summary>
    /// Gets a cached icon for a filename extension.
    /// </summary>
    /// <param name="fileName">The filename.</param>
    /// <param name="large">Whether to request a large icon.</param>
    /// <returns>The icon, or null for a null filename.</returns>
    public static ImageSource? FindIconForFilename(string? fileName, bool large)
    {
        if (fileName is null)
        {
            return null;
        }

        var extension = Path.GetExtension(fileName);
        var cache = large ? LargeIconCache : SmallIconCache;
        if (cache.TryGetValue(extension, out var image))
        {
            return image;
        }

        using var icon = ReadIcon(fileName, 0x80, large ? IconSize.Large : IconSize.Small, false);
        image = icon.ToImageSource();
        cache.Add(extension, image);
        return image;
    }

    /// <summary>
    /// Gets an owned folder icon that the caller must dispose.
    /// </summary>
    /// <param name="size">The requested size.</param>
    /// <param name="folderType">Whether the folder is open.</param>
    /// <returns>The folder icon.</returns>
    public static Icon GetFolderIcon(IconSize size, FolderType folderType) =>
        ReadIcon(Environment.GetFolderPath(Environment.SpecialFolder.Windows), 0x10, size, folderType == FolderType.Open);

    /// <summary>
    /// Copies an icon to a frozen bitmap source.
    /// </summary>
    /// <param name="icon">The icon.</param>
    /// <returns>The copied image.</returns>
    public static ImageSource ToImageSource(this Icon icon)
    {
        ArgumentNullException.ThrowIfNull(icon);
        var image = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        image.Freeze();
        return image;
    }

    private static unsafe Icon ReadIcon(string path, uint attributes, IconSize size, bool open)
    {
        const uint iconFlag = 0x100;
        const uint useAttributes = 0x10;
        var flags = iconFlag | useAttributes | (size == IconSize.Small ? 1U : 0U) | (open ? 2U : 0U);
        var result = NativeMethods.SHGetFileInfo(path, attributes, out var info, (uint)sizeof(NativeMethods.ShellFileInfo), flags);
        if (result == 0 || info.IconHandle == 0)
        {
            throw new Win32Exception("The Windows shell could not retrieve the requested icon.");
        }

        Icon clone;
        bool released;
        try
        {
            using var borrowed = Icon.FromHandle(info.IconHandle);
            clone = (Icon)borrowed.Clone();
        }
        finally
        {
            released = NativeMethods.DestroyIcon(info.IconHandle);
        }

        if (!released)
        {
            clone.Dispose();
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "The Windows icon handle could not be released.");
        }

        return clone;
    }
}
