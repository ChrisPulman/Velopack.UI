// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Velopack.UI.Helpers;

/// <summary>
/// Registers Velopack.UI project files with Windows for the current user.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
internal static class FileAssociationHelper
{
    private const string ProgId = "Velopack.UI.Project";
    private const string Extension = PathFolderHelper.ProjectFileExtension;

    internal static string? GetProjectFileArgument(IEnumerable<string>? args)
    {
        if (args == null)
        {
            return null;
        }

        foreach (var arg in args)
        {
            var path = arg.Trim().Trim('"');
            if (string.Equals(Path.GetExtension(path), Extension, StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            {
                return Path.GetFullPath(path);
            }
        }

        return null;
    }

    internal static void RegisterProjectFileAssociation()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            executablePath = Process.GetCurrentProcess().MainModule?.FileName;
        }

        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return;
        }

        using (var extensionKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{Extension}"))
        {
            extensionKey?.SetValue(null, ProgId);
            extensionKey?.SetValue("Content Type", "application/json");
            extensionKey?.SetValue("PerceivedType", "text");
        }

        using (var progIdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}"))
        {
            progIdKey?.SetValue(null, "Velopack.UI Project");
            progIdKey?.SetValue("FriendlyTypeName", "Velopack.UI Project");
        }

        using (var defaultIconKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}\DefaultIcon"))
        {
            defaultIconKey?.SetValue(null, Quote(executablePath) + ",0");
        }

        using (var commandKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}\shell\open\command"))
        {
            commandKey?.SetValue(null, $"{Quote(executablePath)} \"%1\"");
        }

        NotifyShellAssociationChanged();
    }

    internal static void UnregisterProjectFileAssociation()
    {
        using (var extensionKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{Extension}", writable: true))
        {
            if (extensionKey != null &&
                string.Equals(extensionKey.GetValue(null) as string, ProgId, StringComparison.OrdinalIgnoreCase))
            {
                extensionKey.DeleteValue(string.Empty, throwOnMissingValue: false);
            }
        }

        Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProgId}", throwOnMissingSubKey: false);
        NotifyShellAssociationChanged();
    }

    private static string Quote(string value) => "\"" + value + "\"";

    private static void NotifyShellAssociationChanged()
    {
        try
        {
            NativeMethods.SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
        }
        catch
        {
            // The association is registered even if the shell notification fails.
        }
    }

    private static partial class NativeMethods
    {
        [DllImport("shell32.dll")]
        internal static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
    }
}
