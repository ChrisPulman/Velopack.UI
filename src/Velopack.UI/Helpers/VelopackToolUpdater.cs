// <copyright file="VelopackToolUpdater.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Velopack.UI.Helpers;

/// <summary>Prepares the global packaging tool once for concurrent startup and packaging callers.</summary>
internal sealed class VelopackToolUpdater
{
    private readonly object gate = new();
    private readonly string targetVersion;
    private readonly Func<IReadOnlyList<string>, Task<ToolProcessResult>> execute;
    private Task? readiness;

    /// <summary>Initializes a new instance of the <see cref="VelopackToolUpdater"/> class.</summary>
    /// <param name="targetVersion">The matching Velopack package version.</param>
    /// <param name="execute">The asynchronous dotnet process executor.</param>
    internal VelopackToolUpdater(string targetVersion, Func<IReadOnlyList<string>, Task<ToolProcessResult>> execute)
    {
        this.targetVersion = targetVersion;
        this.execute = execute;
    }

    /// <summary>Gets the shared application tool updater.</summary>
    internal static VelopackToolUpdater Shared { get; } = new(
        typeof(VelopackToolUpdater).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "VelopackToolVersion").Value!,
        RunDotnetAsync);

    /// <summary>Shares in-progress preparation and retries a previous failed attempt.</summary>
    /// <returns>The task that completes only when the required tool is ready.</returns>
    internal Task EnsureReadyAsync()
    {
        lock (this.gate)
        {
            if (this.readiness is null or { IsFaulted: true } or { IsCanceled: true })
            {
                this.readiness = Task.Run(this.PrepareAsync);
            }

            return this.readiness;
        }
    }

    private static async Task<ToolProcessResult> RunDotnetAsync(IReadOnlyList<string> arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetTempPath(),
            },
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        // Tool management must not wait for CLI telemetry or workload advertising on app startup.
        process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        process.StartInfo.Environment["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "true";

        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // The process exited between cancellation and termination.
            }

            await process.WaitForExitAsync().ConfigureAwait(false);
            await Task.WhenAll(output, error).ConfigureAwait(false);
            throw new TimeoutException("Preparing the global vpk tool timed out after five minutes.");
        }

        return new ToolProcessResult(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
    }

    private static string? FindVersion(string output)
    {
        foreach (var line in output.Split('\n'))
        {
            var columns = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (columns.Length >= 2 && columns[0].Equals("vpk", StringComparison.OrdinalIgnoreCase))
            {
                return columns[1];
            }
        }

        return null;
    }

    private static void RequireSuccess(ToolProcessResult result)
    {
        if (result.ExitCode == 0)
        {
            return;
        }

        throw new InvalidOperationException($"dotnet exited with code {result.ExitCode}: {result.StandardError}\n{result.StandardOutput}");
    }

    private async Task PrepareAsync()
    {
        try
        {
            var list = await this.execute(["tool", "list", "--global"]).ConfigureAwait(false);
            RequireSuccess(list);
            var installedVersion = FindVersion(list.StandardOutput);
            if (installedVersion == this.targetVersion)
            {
                return;
            }

            var operation = installedVersion == null ? "install" : "update";
            var arguments = new List<string> { "tool", operation, "--global", "vpk", "--version", this.targetVersion };
            if (installedVersion != null)
            {
                arguments.Add("--allow-downgrade");
            }

            RequireSuccess(await this.execute(arguments).ConfigureAwait(false));
            var verified = await this.execute(["tool", "list", "--global"]).ConfigureAwait(false);
            RequireSuccess(verified);
            if (FindVersion(verified.StandardOutput) != this.targetVersion)
            {
                throw new InvalidOperationException("The installed global vpk version did not match the required version after preparation.");
            }
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"The required vpk {this.targetVersion} tool is not ready. Packaging cannot continue. Check your .NET SDK and NuGet connection, then retry packaging. To repair manually run: dotnet tool update --global vpk --version {this.targetVersion} --allow-downgrade\n{exception.Message}",
                exception);
        }
    }

    /// <summary>The captured dotnet process result.</summary>
    /// <param name="ExitCode">The exit status.</param>
    /// <param name="StandardOutput">The standard output.</param>
    /// <param name="StandardError">The standard error.</param>
    internal readonly record struct ToolProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
