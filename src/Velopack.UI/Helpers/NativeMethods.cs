// <copyright file="NativeMethods.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System.Runtime.InteropServices;

namespace Velopack.UI.Helpers;

/// <summary>
/// Native Windows icon APIs.
/// </summary>
internal static partial class NativeMethods
{
    /// <summary>Calls a Windows icon API.</summary>
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("shell32.dll", EntryPoint = "SHGetFileInfoW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint SHGetFileInfo(string path, uint attributes, out ShellFileInfo info, uint size, uint flags);

    /// <summary>Calls a Windows icon API.</summary>
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyIcon(nint icon);

    /// <summary>Contains the Unicode Windows shell file information.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct ShellFileInfo : IEquatable<ShellFileInfo>
    {
        /// <summary>Native IconHandle data.</summary>
        internal nint IconHandle;

        /// <summary>Native IconIndex data.</summary>
        internal int IconIndex;

        /// <summary>Native Attributes data.</summary>
        internal uint Attributes;

        /// <summary>Native DisplayName data.</summary>
        internal fixed char DisplayName[260];

        /// <summary>Native TypeName data.</summary>
        internal fixed char TypeName[80];

        /// <summary>Compares native file information.</summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        /// <returns>Whether the comparison holds.</returns>
        public static bool operator ==(ShellFileInfo left, ShellFileInfo right) => left.Equals(right);

        /// <summary>Compares native file information.</summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        /// <returns>Whether the comparison holds.</returns>
        public static bool operator !=(ShellFileInfo left, ShellFileInfo right) => !left.Equals(right);

        /// <inheritdoc />
        public readonly bool Equals(ShellFileInfo other)
        {
            var left = this;
            return left.IconHandle == other.IconHandle && left.IconIndex == other.IconIndex && left.Attributes == other.Attributes
                && new ReadOnlySpan<char>(left.DisplayName, 260).SequenceEqual(new ReadOnlySpan<char>(other.DisplayName, 260))
                && new ReadOnlySpan<char>(left.TypeName, 80).SequenceEqual(new ReadOnlySpan<char>(other.TypeName, 80));
        }

        /// <inheritdoc />
        public override readonly bool Equals(object? obj) => obj is ShellFileInfo other && this.Equals(other);

        /// <inheritdoc />
        public override readonly int GetHashCode()
        {
            var value = this;
            var hash = default(HashCode);
            hash.Add(value.IconHandle);
            hash.Add(value.IconIndex);
            hash.Add(value.Attributes);
            hash.AddBytes(MemoryMarshal.AsBytes(new ReadOnlySpan<char>(value.DisplayName, 260)));
            hash.AddBytes(MemoryMarshal.AsBytes(new ReadOnlySpan<char>(value.TypeName, 80)));
            return hash.ToHashCode();
        }
    }
}
