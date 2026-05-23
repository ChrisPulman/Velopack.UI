// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text;

namespace Velopack.UI;

/// <summary>
/// Publishes release assets by delegating authentication and release operations to GitHub CLI.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
internal static class GitHubCliReleasePublisher
{
    internal static async Task PublishAsync(GitHubReleasesConnection connection, IReadOnlyCollection<string> files, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (files.Count == 0)
        {
            throw new InvalidOperationException("No release files were found to upload.");
        }

        var owner = Require(connection.Owner, "GitHub owner is required.");
        var repository = Require(connection.Repository, "GitHub repository is required.");
        var tagName = Require(connection.TagName, "GitHub release tag is required.");
        var repositorySelector = $"{owner}/{repository}";

        await EnsureGitHubCliReadyAsync(cancellationToken).ConfigureAwait(false);

        var releaseExists = await ReleaseExistsAsync(repositorySelector, tagName, cancellationToken).ConfigureAwait(false);
        if (releaseExists)
        {
            await UploadAssetsAsync(repositorySelector, tagName, files, cancellationToken).ConfigureAwait(false);
            return;
        }

        await CreateReleaseAsync(repositorySelector, connection, files, cancellationToken).ConfigureAwait(false);
    }

    internal static void StartInteractiveSignIn()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/k gh auth login --web --hostname github.com --scopes repo",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Normal
        };

        Process.Start(startInfo);
    }

    private static async Task EnsureGitHubCliReadyAsync(CancellationToken cancellationToken)
    {
        var version = await RunGhAsync(["--version"], TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
        if (version.ExitCode != 0)
        {
            throw new InvalidOperationException("GitHub CLI was not found. Install GitHub CLI, then sign in with: gh auth login --web --hostname github.com --scopes repo");
        }

        var authStatus = await RunGhAsync(["auth", "status", "--hostname", "github.com"], TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
        if (authStatus.ExitCode != 0)
        {
            throw new InvalidOperationException("GitHub CLI is not authenticated for github.com. Click 'Sign in with GitHub CLI' or run: gh auth login --web --hostname github.com --scopes repo");
        }
    }

    private static async Task<bool> ReleaseExistsAsync(string repositorySelector, string tagName, CancellationToken cancellationToken)
    {
        var result = await RunGhAsync(
            ["release", "view", tagName, "--repo", repositorySelector],
            TimeSpan.FromSeconds(30),
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode == 0)
        {
            return true;
        }

        var output = result.CombinedOutput;
        if (output.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("could not resolve", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("HTTP 404", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new InvalidOperationException("Unable to inspect GitHub release: " + output);
    }

    private static Task CreateReleaseAsync(string repositorySelector, GitHubReleasesConnection connection, IReadOnlyCollection<string> files, CancellationToken cancellationToken)
    {
        var tagName = Require(connection.TagName, "GitHub release tag is required.");
        var releaseName = string.IsNullOrWhiteSpace(connection.ReleaseName) ? tagName : connection.ReleaseName.Trim();
        var args = new List<string>
        {
            "release",
            "create",
            tagName,
        };

        args.AddRange(files.Select(Path.GetFullPath));
        args.AddRange(["--repo", repositorySelector, "--title", releaseName, "--notes", $"Velopack release {tagName}."]);

        if (connection.Draft)
        {
            args.Add("--draft");
        }

        if (connection.Prerelease)
        {
            args.Add("--prerelease");
        }

        return RunGhCheckedAsync(args, TimeSpan.FromMinutes(30), "GitHub release creation failed", cancellationToken);
    }

    private static Task UploadAssetsAsync(string repositorySelector, string tagName, IReadOnlyCollection<string> files, CancellationToken cancellationToken)
    {
        var args = new List<string>
        {
            "release",
            "upload",
            tagName,
        };

        args.AddRange(files.Select(Path.GetFullPath));
        args.AddRange(["--repo", repositorySelector, "--clobber"]);

        return RunGhCheckedAsync(args, TimeSpan.FromMinutes(30), "GitHub release upload failed", cancellationToken);
    }

    private static async Task RunGhCheckedAsync(IReadOnlyCollection<string> args, TimeSpan timeout, string failurePrefix, CancellationToken cancellationToken)
    {
        var result = await RunGhAsync(args, timeout, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"{failurePrefix}: {result.CombinedOutput}");
        }
    }

    private static async Task<ProcessResult> RunGhAsync(IReadOnlyCollection<string> args, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "gh",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        try
        {
            if (!process.Start())
            {
                return new ProcessResult(-1, string.Empty, "Failed to start GitHub CLI.");
            }
        }
        catch (Win32Exception ex)
        {
            return new ProcessResult(-1, string.Empty, $"Failed to start GitHub CLI: {ex.Message}");
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            using var timeoutToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutToken.CancelAfter(timeout);
            await process.WaitForExitAsync(timeoutToken.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            return new ProcessResult(-1, string.Empty, $"GitHub CLI timed out after {timeout}.");
        }

        return new ProcessResult(
            process.ExitCode,
            await standardOutput.ConfigureAwait(false),
            await standardError.ConfigureAwait(false));
    }

    private static string Require(string? value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException(message) : value.Trim();

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Process may already have exited.
        }
    }

    private readonly record struct ProcessResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string CombinedOutput => string.Join(
            Environment.NewLine,
            new[] { StandardError, StandardOutput }.Where(output => !string.IsNullOrWhiteSpace(output))).Trim();
    }
}
