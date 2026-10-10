// <copyright file="UpdateArtifactTests.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

namespace Velopack.UI.Tests;

/// <summary>Verifies update publishing preserves packages referenced by release feeds.</summary>
public sealed class UpdateArtifactTests
{
    /// <summary>Verifies full packages remain available and generated installers are excluded from update-only publishing.</summary>
    /// <param name="fileName">The release artifact name.</param>
    /// <param name="included">Whether updates require the artifact.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Velopack.UI-1.1.0-full.nupkg", true)]
    [Arguments("Velopack.UI-1.1.0-delta.nupkg", true)]
    [Arguments("releases.win.json", true)]
    [Arguments("assets.win.json", true)]
    [Arguments("RELEASES", true)]
    [Arguments("Setup.exe", false)]
    [Arguments("Velopack.UI-win-Setup.exe", false)]
    [Arguments("Velopack.UI-WIN-SETUP.EXE", false)]
    [Arguments("Velopack.UI-win-Portable.zip", false)]
    [Arguments("Velopack.UI-win-DeploymentTool.MSI", false)]
    public async Task IsUpdateArtifact_PackageAndInstallerFiles_KeepsRequiredPackages(string fileName, bool included) =>
        await Assert.That(VelopackModel.IsUpdateArtifact(fileName)).IsEqualTo(included);
}
