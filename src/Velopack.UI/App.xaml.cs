// <copyright file="App.xaml.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using System.Windows.Threading;
using ReactiveUI;
using ReactiveUI.Builder;
using Splat;
using Velopack.Sources;
using Velopack.UI.Helpers;

namespace Velopack.UI;

/// <summary>
/// Interaction logic for App.xaml.
/// </summary>
public partial class App
{
    /// <summary>
    /// Gets the project file supplied on the command line.
    /// </summary>
    internal static string? StartupProjectFilePath { get; private set; }

    /// <summary>
    /// Raises the <see cref="E:System.Windows.Application.Startup" /> event.
    /// </summary>
    /// <param name="e">A <see cref="T:System.Windows.StartupEventArgs" /> that contains the event data.</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        RxAppBuilder.CreateReactiveUIBuilder().WithWpf().Build();

        base.OnStartup(e);

        // Check if running as administrator
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        if (principal.IsInRole(WindowsBuiltInRole.Administrator))
        {
            var r = MessageBox.Show("Do not run Velopack.UI as Administrator. Windows drag and drop is restricted in elevated apps and will not work.", "Administrator Detected", MessageBoxButton.OK, MessageBoxImage.Warning);
            if (r == MessageBoxResult.OK)
            {
                Environment.Exit(1);
            }
        }

        _ = this.Dispatcher.BeginInvoke(new Action(QueuePostStartupChecks), DispatcherPriority.ApplicationIdle);
    }

    [STAThread]
    private static void Main(string[] args)
    {
        StartupProjectFilePath = FileAssociationHelper.GetProjectFileArgument(args);

        VelopackApp.Build()
            .SetArgs(args)
            .OnAfterInstallFastCallback(_ => FileAssociationHelper.RegisterProjectFileAssociation())
            .OnAfterUpdateFastCallback(_ => FileAssociationHelper.RegisterProjectFileAssociation())
            .OnBeforeUninstallFastCallback(_ => FileAssociationHelper.UnregisterProjectFileAssociation())
            .OnFirstRun(_ => FileAssociationHelper.RegisterProjectFileAssociation())
            .Run();

        App app = new();
        app.InitializeComponent();
        app.Run();
    }

    private static void QueueVelopackToolCheck() =>
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                await TryPromptVelopackToolAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Velopack tool check failed: {ex}");
            }
        });

    private static async Task TryPromptVelopackToolAsync()
    {
        var listResult = await RunProcessAsync("dotnet", TimeSpan.FromSeconds(30), "tool", "list", "-g").ConfigureAwait(false);
        if (listResult.ExitCode != 0)
        {
            Trace.TraceWarning($"Failed to list global dotnet tools: {listResult.StandardError}{listResult.StandardOutput}");
            return;
        }

        if (listResult.StandardOutput.Contains("vpk", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var rslt = await ShowMessageBoxAsync(
            "The Velopack global tool 'vpk' is not installed. Install now?\nThis runs: dotnet tool install -g vpk",
            "Velopack Tool Missing",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information).ConfigureAwait(false);

        if (rslt != MessageBoxResult.Yes)
        {
            return;
        }

        var installResult = await RunProcessAsync("dotnet", TimeSpan.FromMinutes(2), "tool", "install", "-g", "vpk").ConfigureAwait(false);
        if (installResult.ExitCode == 0)
        {
            return;
        }

        await ShowMessageBoxAsync(
            $"Failed to install vpk:\n{installResult.StandardError}\n{installResult.StandardOutput}",
            "Install Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error).ConfigureAwait(false);
    }

    private static void QueuePostStartupChecks()
    {
        QueueVelopackToolCheck();
        QueueUpdateCheck();
    }

    private static void QueueUpdateCheck() =>
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                await UpdateMyApp().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Velopack update check failed: {ex}");
            }
        });

    private static async Task UpdateMyApp()
    {
        var source = new GithubSource("https://github.com/ChrisPulman/Velopack.UI", accessToken: null, prerelease: false);
        var mgr = new UpdateManager(source);
        if (!mgr.IsInstalled)
        {
            return; // not installed with Velopack, so skip update check
        }

        // A previously downloaded update remains usable when the network is offline.
        var updateToApply = mgr.UpdatePendingRestart;
        if (updateToApply == null)
        {
            var newVersion = await WithTimeout(mgr.CheckForUpdatesAsync(), TimeSpan.FromSeconds(30), "checking for updates").ConfigureAwait(false);
            if (newVersion == null)
            {
                return;
            }

            using var downloadTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            await mgr.DownloadUpdatesAsync(newVersion, cancelToken: downloadTimeout.Token).ConfigureAwait(false);
            updateToApply = newVersion.TargetFullRelease;
        }

        // Keep the final check and restart together on the UI thread so a build
        // cannot start between checking its state and closing the application.
        var dispatcher = Current?.Dispatcher;
        if (dispatcher?.HasShutdownStarted != false)
        {
            return;
        }

        await dispatcher.InvokeAsync(() =>
        {
            var viewModel = Locator.Current.GetService<MainViewModel>();
            if (viewModel?.IsBusy == true)
            {
                return; // The downloaded update will be available next launch.
            }

            var message = viewModel?.HasUnsavedChanges == true
                ? "An update is ready. Save your project before restarting. Restart now and discard unsaved changes?"
                : "An update is ready. Restart Velopack.UI now to install it?";
            if (ShowMessageBox(message, "Update ready", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes)
            {
                return;
            }

            mgr.ApplyUpdatesAndRestart(updateToApply);
        }).Task.ConfigureAwait(false);
    }

    private static async Task<T?> WithTimeout<T>(Task<T?> task, TimeSpan timeout, string operation)
    {
        try
        {
            return await task.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Trace.TraceWarning($"Velopack timed out while {operation} after {timeout}.");
            _ = ObserveTimedOutTaskAsync(task);
            return default;
        }
    }

    private static async Task ObserveTimedOutTaskAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Velopack background operation failed after timing out: {ex}");
        }
    }

    private static async Task<ProcessResult> RunProcessAsync(string fileName, TimeSpan timeout, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (!process.Start())
        {
            return new ProcessResult(-1, string.Empty, $"Failed to start '{fileName}'.");
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        try
        {
            using var timeoutToken = new CancellationTokenSource(timeout);
            await process.WaitForExitAsync(timeoutToken.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            return new ProcessResult(-1, string.Empty, $"'{fileName}' timed out after {timeout}.");
        }

        return new ProcessResult(
            process.ExitCode,
            await standardOutput.ConfigureAwait(false),
            await standardError.ConfigureAwait(false));
    }

    private static Task<MessageBoxResult> ShowMessageBoxAsync(string message, string title, MessageBoxButton buttons, MessageBoxImage image)
    {
        var dispatcher = Current?.Dispatcher;
        if (dispatcher?.CheckAccess() != false)
        {
            return Task.FromResult(ShowMessageBox(message, title, buttons, image));
        }

        return dispatcher.InvokeAsync(() => ShowMessageBox(message, title, buttons, image)).Task;
    }

    private static MessageBoxResult ShowMessageBox(string message, string title, MessageBoxButton buttons, MessageBoxImage image)
    {
        var owner = Current?.MainWindow;
        return owner == null
            ? MessageBox.Show(message, title, buttons, image)
            : MessageBox.Show(owner, message, title, buttons, image);
    }

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

    private readonly record struct ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
