// <copyright file="CheckInternetConnection.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Velopack.UI;

/// <summary>
/// Check Internet Connection.
/// </summary>
public static partial class CheckInternetConnection
{
    /// <summary>
    /// Determines whether [is connected to internet].
    /// </summary>
    /// <returns><c>true</c> if [is connected to internet]; otherwise, <c>false</c>.</returns>
    public static bool IsConnectedToInternet()
    {
        try
        {
            return NativeMethods.InternetGetConnectedState(out _, 0);
        }
        catch
        {
            Debug.WriteLine("Problem with the connection to the Internet");
        }

        return false;
    }

    private static partial class NativeMethods
    {
        [LibraryImport("wininet.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool InternetGetConnectedState(out int description, int reservedValue);
    }
}
