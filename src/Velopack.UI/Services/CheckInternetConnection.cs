// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Velopack.UI;

/// <summary>
/// Check Internet Connection.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1060:Move pinvokes to native methods class", Justification = "intended")]
public static class CheckInternetConnection
{
    /// <summary>
    /// Determines whether [is connected to internet].
    /// </summary>
    /// <returns><c>true</c> if [is connected to internet]; otherwise, <c>false</c>.</returns>
    public static bool IsConnectedToInternet()
    {
        try
        {
            return InternetGetConnectedState(out var desc, 0);
        }
        catch
        {
            Debug.WriteLine("Problem with the connection to the Internet");
        }

        return false;
    }

    // Creating the extern function…
    [DllImport("wininet.dll")]
    private static extern bool InternetGetConnectedState(out int description, int reservedValue);
}
