// <copyright file="FileAssociationHelperTests.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System.IO;
using Velopack.UI.Helpers;

namespace Velopack.UI.Tests;

/// <summary>Verifies startup resolves project arguments without changing Windows file associations.</summary>
public sealed class FileAssociationHelperTests
{
    /// <summary>Verifies startup tolerates an absent command line.</summary>
    /// <returns>A task that completes after the assertion.</returns>
    [Test]
    public async Task GetProjectFileArgument_NoArguments_ReturnsNull() =>
        await Assert.That(FileAssociationHelper.GetProjectFileArgument(null)).IsNull();

    /// <summary>Verifies shell quoting, mixed case extensions, and unrelated arguments are handled.</summary>
    /// <returns>A task that completes after the assertions.</returns>
    [Test]
    public async Task GetProjectFileArgument_QuotedProjects_ReturnsFirstExistingProject()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var project = Path.Combine(directory, "My Project.VELO");
            var secondProject = Path.Combine(directory, "Second.velo");
            var unrelated = Path.Combine(directory, "notes.txt");
            File.WriteAllText(project, "{}");
            File.WriteAllText(secondProject, "{}");
            File.WriteAllText(unrelated, "notes");

            var result = FileAssociationHelper.GetProjectFileArgument([
                "--some-switch",
                unrelated,
                Path.Combine(directory, "missing.velo"),
                $"  \"{project}\"  ",
                secondProject,
            ]);

            await Assert.That(result).IsEqualTo(Path.GetFullPath(project));
            await Assert.That(File.ReadAllText(project)).IsEqualTo("{}");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
