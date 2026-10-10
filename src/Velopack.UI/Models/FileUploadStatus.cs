// <copyright file="FileUploadStatus.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

namespace Velopack.UI.Models;

/// <summary>
/// File Upload Status.
/// </summary>
public enum FileUploadStatus
{
    /// <summary>
    /// The queued.
    /// </summary>
    Queued,

    /// <summary>
    /// The in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// The completed.
    /// </summary>
    Completed,

    /// <summary>
    /// The failed.
    /// </summary>
    Failed,
}
