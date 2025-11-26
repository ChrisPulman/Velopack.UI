// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
