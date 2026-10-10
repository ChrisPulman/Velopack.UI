// <copyright file="GitHubCliReleasePublisherTests.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

namespace Velopack.UI.Tests;

/// <summary>
/// Verifies application behavior and input validation.
/// </summary>
public sealed class GitHubCliReleasePublisherTests
{
    /// <summary>
    /// Verifies an empty upload is rejected before invoking GitHub CLI.
    /// </summary>
    /// <returns>A task that completes after the assertions.</returns>
    [Test]
    public async Task PublishAsync_NoAssets_RejectsBeforeStartingGitHubCli()
    {
        var connection = new GitHubReleasesConnection();

        var exception = await Assert.That(() => GitHubCliReleasePublisher.PublishAsync(connection, []))
            .Throws<InvalidOperationException>();

        await Assert.That(exception?.Message).IsEqualTo("No release files were found to upload.");
    }

    /// <summary>
    /// Verifies incomplete release identity is rejected before invoking GitHub CLI.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="tag">The release tag.</param>
    /// <param name="expected">The expected error message.</param>
    /// <returns>A task that completes after the assertions.</returns>
    [Test]
    [Arguments(null, "repository", "v1", "GitHub owner is required.")]
    [Arguments("owner", " ", "v1", "GitHub repository is required.")]
    [Arguments("owner", "repository", "", "GitHub release tag is required.")]
    public async Task PublishAsync_MissingReleaseIdentity_RejectsBeforeStartingGitHubCli(string? owner, string? repository, string? tag, string expected)
    {
        var connection = new GitHubReleasesConnection { Owner = owner, Repository = repository, TagName = tag };

        var exception = await Assert.That(() => GitHubCliReleasePublisher.PublishAsync(connection, ["release.nupkg"]))
            .Throws<InvalidOperationException>();

        await Assert.That(exception?.Message).IsEqualTo(expected);
    }
}
