using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Windows;
using ReactiveUI;

namespace Velopack.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Ensure Velopack bootstrap is invoked very early so vpk can validate it
        try
        {
            VelopackApp.Build().Run();
        }
        catch
        {
            // non-fatal at runtime; packaging will still validate presence
        }

        base.OnStartup(e);
        TryPromptVelopackTool();
        RxApp.TaskpoolScheduler.Schedule(async () => await UpdateMyApp("D:\\Installers\\Releases"));
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
            if (outText.Contains("vpk") == false)
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
        if (mgr.IsInstalled == false)
            return; // not installed with Velopack, so skip update check

        // check for new version
        var newVersion = await mgr.CheckForUpdatesAsync();
        if (newVersion == null)
            return; // no update available

        // download new version
        await mgr.DownloadUpdatesAsync(newVersion);

        // install new version and restart app
        mgr.ApplyUpdatesAndRestart(newVersion);
    }
}
