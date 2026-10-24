// <copyright file="VelopackModelTests.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System.IO;

namespace Velopack.UI.Tests;

/// <summary>Verifies local package staging and legacy project settings.</summary>
public sealed class VelopackModelTests
{
    /// <summary>Verifies rebuilding staging preserves release artifacts required for subsequent delta builds.</summary>
    /// <returns>A task that completes after the assertions.</returns>
    [Test]
    public async Task ClearPackageFilesDirectory_RemovesStagingAndPreservesReleases()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var model = new VelopackModel();
            model.SetOutputBasePath(directory);
            var staging = Path.Combine(directory, "PackageFiles");
            var releases = Path.Combine(directory, "Releases");
            Directory.CreateDirectory(Path.Combine(staging, "nested"));
            Directory.CreateDirectory(releases);
            File.WriteAllText(Path.Combine(staging, "nested", "stale.dll"), "old build");
            var release = Path.Combine(releases, "Sample-1.0.0-full.nupkg");
            File.WriteAllText(release, "previous release");

            model.ClearPackageFilesDirectory();

            await Assert.That(model.PackageFilesOutputPath).IsEqualTo(staging);
            await Assert.That(model.VelopackOutputPath).IsEqualTo(releases);
            await Assert.That(Directory.Exists(staging)).IsTrue();
            await Assert.That(Directory.EnumerateFileSystemEntries(staging).Any()).IsFalse();
            await Assert.That(File.ReadAllText(release)).IsEqualTo("previous release");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>Verifies old projects disabling delta generation retain that choice when loaded.</summary>
    /// <param name="savedMode">The stored delta mode.</param>
    /// <param name="expectedMode">The restored effective mode.</param>
    /// <returns>A task that completes after the assertion.</returns>
    [Test]
    [Arguments(null, "None")]
    [Arguments("BestSpeed", "None")]
    [Arguments("BestSize", "BestSize")]
    public async Task ResyncAfterLoad_LegacyDeltaSettings_PreservesEffectiveMode(string? savedMode, string expectedMode)
    {
        var model = new VelopackModel
        {
            GenerateDeltaPackages = false,
            DeltaMode = savedMode,
        };

        model.ResyncAfterLoad();

        await Assert.That(model.GetResolvedDeltaMode()).IsEqualTo(expectedMode);
    }
}
