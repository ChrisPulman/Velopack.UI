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
        if (args.Length > 0 && args[0] == "--safesign-file")
        {
            try
            {
                if (args.Length != 2)
                {
                    throw new ArgumentException("The signing helper requires exactly one file.");
                }

                SimplySignSigning.SignFileAsync(args[1], new ProcessStartInfo()).GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.Message);
                Environment.ExitCode = 1;
            }

            return;
        }

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
        _ = ObserveVelopackToolReadinessAsync();

    private static async Task ObserveVelopackToolReadinessAsync()
    {
        try
        {
            await VelopackToolUpdater.Shared.EnsureReadyAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Trace.TraceWarning($"Velopack tool preparation failed: {exception}");
            await ShowMessageBoxAsync(exception.Message, "Velopack tool preparation failed", MessageBoxButton.OK, MessageBoxImage.Error).ConfigureAwait(false);
        }
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
}
