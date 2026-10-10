// <copyright file="GitHubReleasesConnectionTests.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System.Text.Json;

namespace Velopack.UI.Tests;

/// <summary>
/// Verifies application behavior and input validation.
/// </summary>
public sealed class GitHubReleasesConnectionTests
{
    /// <summary>Verifies updater and latest setup links stay permanent when the publishing tag changes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DownloadUrls_TagChanges_KeepPermanentUpdateAndLatestSetupUrls()
    {
        var connection = new GitHubReleasesConnection
        {
            Owner = "owner", Repository = "application", TagName = "v1", PackageTitle = "My App", PackageChannel = "beta",
        };
        var notifications = new List<string?>();
        connection.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        connection.TagName = "v2";

        await Assert.That(connection.UpdateSourceUrl).IsEqualTo("https://github.com/owner/application");
        await Assert.That(connection.LatestSetupDownloadUrl).IsEqualTo("https://github.com/owner/application/releases/latest/download/My%20App-beta-Setup.exe");
        await Assert.That(connection.SetupDownloadUrl).IsEqualTo("https://github.com/owner/application/releases/download/v2/My%20App-beta-Setup.exe");

        connection.PackageChannel = "win";

        await Assert.That(connection.LatestSetupDownloadUrl).IsEqualTo("https://github.com/owner/application/releases/latest/download/My%20App-win-Setup.exe");
        await Assert.That(notifications).Contains(nameof(GitHubReleasesConnection.LatestSetupDownloadUrl));
    }

    /// <summary>Verifies a publishing tag is unnecessary for permanent links and missing repository details are reported.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DownloadUrls_NoPublishingTag_AllowPermanentRepositoryLinks()
    {
        var connection = new GitHubReleasesConnection { Owner = "owner", Repository = "application" };

        await Assert.That(connection.UpdateSourceUrl).IsEqualTo("https://github.com/owner/application");
        await Assert.That(connection.LatestSetupDownloadUrl).IsEqualTo("https://github.com/owner/application/releases/latest/download/application-win-Setup.exe");
        await Assert.That(connection.SetupDownloadUrl).IsEqualTo("Missing Parameter");

        connection.Owner = null;

        await Assert.That(connection.UpdateSourceUrl).IsEqualTo("Missing Parameter");
        await Assert.That(connection.LatestSetupDownloadUrl).IsEqualTo("Missing Parameter");
    }

    /// <summary>
    /// Verifies GitHub release links populate the owner, repository and decoded tag.
    /// </summary>
    /// <returns>A task that completes after the assertions.</returns>
    [Test]
    public async Task RepositoryUrl_ReleaseLink_ParsesRepositoryAndDecodedTag()
    {
        var connection = new GitHubReleasesConnection
        {
            RepositoryUrl = "https://github.com/owner/application.git/releases/tag/v1%20preview",
        };

        await Assert.That(connection.Owner).IsEqualTo("owner");
        await Assert.That(connection.Repository).IsEqualTo("application");
        await Assert.That(connection.TagName).IsEqualTo("v1 preview");
    }

    /// <summary>
    /// Verifies other websites cannot replace the release identity.
    /// </summary>
    /// <returns>A task that completes after the assertions.</returns>
    [Test]
    public async Task RepositoryUrl_OtherHost_KeepsExistingReleaseIdentity()
    {
        var connection = new GitHubReleasesConnection
        {
            Owner = "owner",
            Repository = "application",
            TagName = "v1",
            RepositoryUrl = "https://example.com/another/repository/releases/tag/v2",
        };

        await Assert.That(connection.Owner).IsEqualTo("owner");
        await Assert.That(connection.Repository).IsEqualTo("application");
        await Assert.That(connection.TagName).IsEqualTo("v1");
    }

    /// <summary>Verifies repository URLs are reconstructed after serialization and track direct owner edits.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RepositoryUrl_RoundTripAndOwnerEdits_ReflectRepositoryIdentity()
    {
        WebConnectionBase original = new GitHubReleasesConnection
        {
            Owner = "owner",
            Repository = "application",
            TagName = "v1",
        };

        var json = JsonSerializer.Serialize(original);
        await Assert.That(json.Contains(nameof(GitHubReleasesConnection.RepositoryUrl), StringComparison.Ordinal)).IsFalse();
        var restored = JsonSerializer.Deserialize<WebConnectionBase>(json) as GitHubReleasesConnection;

        await Assert.That(restored).IsNotNull();
        await Assert.That(restored!.RepositoryUrl).IsEqualTo("https://github.com/owner/application");
        await Assert.That(restored.TagName).IsEqualTo("v1");

        var legacy = JsonSerializer.Deserialize<WebConnectionBase>(
            "{\"$type\":\"GitHubReleasesConnection\",\"Owner\":\"legacy-owner\",\"Repository\":\"legacy-app\",\"TagName\":\"v2\"}") as GitHubReleasesConnection;
        await Assert.That(legacy).IsNotNull();
        await Assert.That(legacy!.RepositoryUrl).IsEqualTo("https://github.com/legacy-owner/legacy-app");
        await Assert.That(legacy.TagName).IsEqualTo("v2");

        var notifications = new List<string?>();
        restored.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        restored.Owner = "another-owner";

        await Assert.That(restored.RepositoryUrl).IsEqualTo("https://github.com/another-owner/application");
        await Assert.That(notifications).Contains(nameof(GitHubReleasesConnection.RepositoryUrl));
        await Assert.That(restored.TagName).IsEqualTo("v1");
    }

    /// <summary>
    /// Verifies setup URLs escape tag and asset names.
    /// </summary>
    /// <returns>A task that completes after the assertions.</returns>
    [Test]
    public async Task SetupDownloadUrl_TagAndTitleWithSpaces_EscapesAssetAndTag()
    {
        var connection = new GitHubReleasesConnection
        {
            Owner = "owner",
            Repository = "application",
            TagName = "v1 preview",
            PackageTitle = "My App",
            PackageChannel = "beta",
        };

        await Assert.That(connection.SetupDownloadUrl)
            .IsEqualTo("https://github.com/owner/application/releases/download/v1%20preview/My%20App-beta-Setup.exe");
    }

    /// <summary>
    /// Verifies CLI authentication needs no token in the project.
    /// </summary>
    /// <returns>A task that completes after the assertions.</returns>
    [Test]
    public async Task Validate_CliAuthentication_DoesNotRequireStoredToken()
    {
        var connection = new GitHubReleasesConnection { Owner = "owner", Repository = "application", TagName = "v1" };

        await Assert.That(connection.Validate().IsValid).IsTrue();

        connection.UseGitHubCliAuthentication = false;

        await Assert.That(connection.Validate().IsValid).IsFalse();
    }
}
