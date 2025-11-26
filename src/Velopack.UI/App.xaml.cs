// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Security.Principal;
using System.Windows;
using ReactiveUI;

namespace Velopack.UI;

/// <summary>
/// Interaction logic for App.xaml.
/// </summary>
public partial class App
{
    /// <summary>
    /// Raises the <see cref="E:System.Windows.Application.Startup" /> event.
    /// </summary>
    /// <param name="e">A <see cref="T:System.Windows.StartupEventArgs" /> that contains the event data.</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Check if running as administrator
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        if (principal.IsInRole(WindowsBuiltInRole.Administrator))
        {
            var r = MessageBox.Show("Do not run Velopack.UI as Administrator. Windows drag and drop is restricted in elevated apps and will not work.", "Administrator Detected", MessageBoxButton.OK, MessageBoxImage.Warning);
            if (r == MessageBoxResult.OK)
            {
                Environment.Exit(1);
            }
        }

        TryPromptVelopackTool();
        RxApp.TaskpoolScheduler.Schedule(async () => await UpdateMyApp("https://github.com/ChrisPulman/Velopack.UI/releases"));
    }

    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build().Run();
        App app = new();
        app.InitializeComponent();
        app.Run();
    }

    private static void TryPromptVelopackTool()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "tool list -g",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            var outText = p!.StandardOutput.ReadToEnd();
            p.WaitForExit();
            if (!outText.Contains("vpk"))
            {
                var rslt = MessageBox.Show("The Velopack global tool 'vpk' is not installed. Install now?\nThis runs: dotnet tool install -g vpk", "Velopack Tool Missing", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (rslt == MessageBoxResult.Yes)
                {
                    var install = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = "tool install -g vpk",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    using var i = Process.Start(install);
                    var stdout = i!.StandardOutput.ReadToEnd();
                    var stderr = i!.StandardError.ReadToEnd();
                    i.WaitForExit();
                    if (i.ExitCode != 0)
                    {
                        MessageBox.Show($"Failed to install vpk:\n{stderr}\n{stdout}", "Install Error");
                    }
                }
            }
        }
        catch
        {
            // non-fatal
        }
    }

    private static async Task UpdateMyApp(string updatePath)
    {
        var mgr = new UpdateManager(updatePath);
        if (!mgr.IsInstalled)
        {
            return; // not installed with Velopack, so skip update check
        }

        // check for new version
        var newVersion = await mgr.CheckForUpdatesAsync();
        if (newVersion == null)
        {
            return; // no update available
        }

        // download new version
        await mgr.DownloadUpdatesAsync(newVersion);

        // install new version and restart app
        mgr.ApplyUpdatesAndRestart(newVersion);
    }
}
