// <copyright file="VelopackToolUpdaterTests.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using Velopack.UI.Helpers;
using static Velopack.UI.Helpers.VelopackToolUpdater;

namespace Velopack.UI.Tests;

/// <summary>Verifies tool preparation without executing dotnet or accessing NuGet.</summary>
public sealed class VelopackToolUpdaterTests
{
    /// <summary>Verifies the installed matching tool needs no modification.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task MatchingTool_SkipsModification()
    {
        var calls = 0;
        var updater = new VelopackToolUpdater("1.2.161", _ =>
        {
            calls++;
            return Task.FromResult(new ToolProcessResult(0, "vpk 1.2.161 vpk", string.Empty));
        });
        await updater.EnsureReadyAsync();
        await updater.EnsureReadyAsync();
        await Assert.That(calls).IsEqualTo(1);
    }

    /// <summary>Verifies install and update target the package version and verify the result.</summary>
    /// <param name="installed">The installed tool listing.</param>
    /// <param name="operation">The required operation.</param>
    /// <param name="downgrade">Whether the update permits matching an older required version.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments("", "install", false)]
    [Arguments("vpk 0.0.1298 vpk", "update", true)]
    [Arguments("vpk 9.0.0 vpk", "update", true)]
    [Arguments("other-vpk 1.2.161 other-vpk", "install", false)]
    public async Task MismatchedTool_PreparesRequiredVersion(string installed, string operation, bool downgrade)
    {
        var calls = new List<string>();
        var updater = new VelopackToolUpdater("1.2.161", arguments =>
        {
            calls.Add(string.Join(' ', arguments));
            return Task.FromResult(new ToolProcessResult(0, calls.Count == 1 ? installed : "vpk 1.2.161 vpk", string.Empty));
        });
        await updater.EnsureReadyAsync();
        await Assert.That(calls.Count).IsEqualTo(3);
        await Assert.That(calls[1]).IsEqualTo($"tool {operation} --global vpk --version 1.2.161{(downgrade ? " --allow-downgrade" : string.Empty)}");
        await Assert.That(calls[2]).IsEqualTo("tool list --global");
    }

    /// <summary>Verifies concurrent callers await one tool preparation.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task ConcurrentCallers_ShareReadiness()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var updater = new VelopackToolUpdater("1.2.161", async _ =>
        {
            Interlocked.Increment(ref calls);
            entered.SetResult();
            await release.Task;
            return new ToolProcessResult(0, "vpk 1.2.161 vpk", string.Empty);
        });
        var startup = updater.EnsureReadyAsync();
        await entered.Task;
        var packaging = updater.EnsureReadyAsync();
        await Assert.That(ReferenceEquals(startup, packaging)).IsTrue();
        await Assert.That(packaging.IsCompleted).IsFalse();
        release.SetResult();
        await Task.WhenAll(startup, packaging);
        await Assert.That(calls).IsEqualTo(1);
    }

    /// <summary>Verifies a failed update blocks readiness and a subsequent request can retry.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task FailedUpdate_RetryCanRecover()
    {
        var calls = 0;
        var updater = new VelopackToolUpdater("1.2.161", _ =>
        {
            var call = Interlocked.Increment(ref calls);
            return Task.FromResult(call switch
            {
                1 => new ToolProcessResult(0, "vpk 0.0.1298 vpk", string.Empty),
                2 => new ToolProcessResult(1, string.Empty, "NuGet unavailable"),
                _ => new ToolProcessResult(0, "vpk 1.2.161 vpk", string.Empty),
            });
        });
        await Assert.That(async () => await updater.EnsureReadyAsync()).Throws<InvalidOperationException>();
        await updater.EnsureReadyAsync();
        await Assert.That(calls).IsEqualTo(3);
    }

    /// <summary>Verifies failed listing or a stale version after update cannot permit packaging.</summary>
    /// <param name="exitCode">The tool listing status.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task UnverifiedTool_BlocksReadiness(int exitCode)
    {
        var updater = new VelopackToolUpdater("1.2.161", _ => Task.FromResult(new ToolProcessResult(exitCode, "vpk 0.0.1298 vpk", "failure")));
        await Assert.That(async () => await updater.EnsureReadyAsync()).Throws<InvalidOperationException>();
    }
}
