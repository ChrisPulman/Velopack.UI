// <copyright file="UploadCompleteEventArgs.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

namespace Velopack.UI;

/// <summary>
/// Upload Complete EventArgs.
/// </summary>
/// <seealso cref="EventArgs"/>
/// <remarks>
/// Initializes a new instance of the <see cref="UploadCompleteEventArgs"/> class.
/// </remarks>
/// <param name="sfu">The sfu.</param>
public class UploadCompleteEventArgs(SingleFileUpload sfu) : EventArgs
{
    /// <summary>
    /// Gets the file uploaded.
    /// </summary>
    /// <value>The file uploaded.</value>
    public SingleFileUpload FileUploaded { get; internal set; } = sfu;
}
