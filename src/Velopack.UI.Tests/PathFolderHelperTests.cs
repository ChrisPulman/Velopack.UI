// <copyright file="PathFolderHelperTests.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System.IO;
using System.Text.Json;
using Velopack.UI.Helpers;

namespace Velopack.UI.Tests;

/// <summary>
/// Verifies application behavior and input validation.
/// </summary>
public sealed class PathFolderHelperTests
{
    /// <summary>Verifies recent projects survive a preference save and reload.</summary>
    /// <returns>A task that completes after the assertion.</returns>
    [Test]
    public async Task Preferences_Roundtrip_RestoresRecentProject()
    {
        var original = new Preference();
        original.LastOpenedProject.Add("C:\\Projects\\Example.velo");
        var restored = JsonSerializer.Deserialize<Preference>(JsonSerializer.Serialize(original));
        await Assert.That(restored!.LastOpenedProject).IsEquivalentTo(original.LastOpenedProject);
    }

    /// <summary>
    /// Verifies Windows filenames lose invalid characters and trailing dots.
    /// </summary>
    /// <param name="filename">The candidate filename.</param>
    /// <param name="expected">The sanitized filename.</param>
    /// <returns>A task that completes after the assertions.</returns>
    [Test]
    [Arguments("release<>:name?.exe", "releasename.exe")]
    [Arguments("project...", "project")]
    [Arguments("My Project-1.0.velo", "My Project-1.0.velo")]
    public async Task ValidateFilename_RemovesInvalidCharactersAndTrailingDots(string filename, string expected)
    {
        await Assert.That(PathFolderHelper.ValidateFilename(filename)).IsEqualTo(expected);
    }

    /// <summary>
    /// Verifies existing project files are preserved when choosing a new path.
    /// </summary>
    /// <returns>A task that completes after the assertions.</returns>
    [Test]
    public async Task GetCorrectFilepath_ExistingFiles_SelectsFirstAvailableSuffix()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "app.velo"), "existing project");
            File.WriteAllText(Path.Combine(directory, "app_0.velo"), "existing copy");

            var result = PathFolderHelper.GetCorrectFilepath(directory, "app", "velo");

            await Assert.That(result).IsEqualTo(Path.Combine(directory, "app_1.velo"));
            await Assert.That(File.ReadAllText(Path.Combine(directory, "app.velo"))).IsEqualTo("existing project");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
