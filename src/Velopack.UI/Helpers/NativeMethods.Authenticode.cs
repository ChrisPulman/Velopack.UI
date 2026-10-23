// <copyright file="NativeMethods.Authenticode.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Velopack.UI.Helpers;

/// <summary>Distinguishes an intact untrusted signature from a damaged Authenticode signature.</summary>
[SupportedOSPlatform("windows10.0.19041.0")]
internal static unsafe partial class NativeMethods
{
    /// <summary>Checks the signature using Windows' Authenticode trust provider.</summary>
    /// <param name="file">The signed file.</param>
    /// <returns>The numeric Windows trust result.</returns>
    internal static uint VerifyAuthenticode(string file)
    {
        var action = new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
        fixed (char* path = file)
        {
            var fileInfo = new TrustFileInfo { Size = (uint)sizeof(TrustFileInfo), FilePath = path };
            var data = new TrustData
            {
                Size = (uint)sizeof(TrustData),
                UiChoice = 2, // WTD_UI_NONE.
                UnionChoice = 1, // WTD_CHOICE_FILE.
                FileInfo = &fileInfo,
                StateAction = 1, // WTD_STATEACTION_VERIFY.
                ProviderFlags = 0x1000, // WTD_CACHE_ONLY_URL_RETRIEVAL: no network access during inspection.
            };
            try
            {
                return WinVerifyTrust(-1, &action, &data);
            }
            finally
            {
                data.StateAction = 2; // WTD_STATEACTION_CLOSE releases the provider state.
                _ = WinVerifyTrust(-1, &action, &data);
            }
        }
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("wintrust.dll", EntryPoint = "WinVerifyTrust")]
    private static partial uint WinVerifyTrust(nint window, Guid* action, TrustData* data);

    [StructLayout(LayoutKind.Sequential)]
    private struct TrustFileInfo
    {
        internal uint Size;
        internal char* FilePath;
        internal nint FileHandle;
        internal nint KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TrustData
    {
        internal uint Size;
        internal nint PolicyCallback;
        internal nint SipClient;
        internal uint UiChoice;
        internal uint RevocationChecks;
        internal uint UnionChoice;
        internal TrustFileInfo* FileInfo;
        internal uint StateAction;
        internal nint StateData;
        internal nint UrlReference;
        internal uint ProviderFlags;
        internal uint UiContext;
        internal nint SignatureSettings;
    }
}
