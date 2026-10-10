// <copyright file="FileAssociationHelper.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

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
internal static partial class FileAssociationHelper
{
    private const string ProgId = "Velopack.UI.Project";
    private const string Extension = PathFolderHelper.ProjectFileExtension;

    /// <summary>Finds the first existing project file supplied on the command line.</summary>
    /// <param name="args">The command line arguments.</param>
    /// <returns>The full project path, or null when no project is supplied.</returns>
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

    /// <summary>Registers the running executable as the current user's project file handler.</summary>
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

    /// <summary>Removes the current user's project file registration for this application.</summary>
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
        [LibraryImport("shell32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
    }
}
