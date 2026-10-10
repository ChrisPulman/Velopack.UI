// <copyright file="FileSystemConnectionTests.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System.IO;

namespace Velopack.UI.Tests;

/// <summary>
/// Verifies application behavior and input validation.
/// </summary>
public sealed class FileSystemConnectionTests
{
    /// <summary>
    /// Verifies releases stay beneath the selected directory.
    /// </summary>
    /// <returns>A task that completes after the assertions.</returns>
    [Test]
    public async Task SetupDownloadUrl_BasePathWithSpaces_KeepsReleasesInsideBasePath()
    {
        var connection = new FileSystemConnection { FileSystemPath = @"C:\My Applications\Installer" };

        await Assert.That(connection.SetupDownloadUrl).IsEqualTo(Path.Combine(connection.FileSystemPath, "Releases"));
    }

    /// <summary>
    /// Verifies missing publishing directories are rejected.
    /// </summary>
    /// <param name="basePath">The configured publishing directory.</param>
    /// <returns>A task that completes after the assertions.</returns>
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task SetupDownloadUrl_MissingBasePath_ReportsMissingParameter(string? basePath)
    {
        var connection = new FileSystemConnection { FileSystemPath = basePath };

        await Assert.That(connection.SetupDownloadUrl).IsEqualTo("Missing Parameter");
        await Assert.That(connection.Validate().IsValid).IsFalse();
    }
}
