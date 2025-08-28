using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Velopack.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        TryPromptVelopackTool();
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
}
