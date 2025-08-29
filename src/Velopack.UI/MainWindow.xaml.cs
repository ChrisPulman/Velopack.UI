using CrissCross;
using CrissCross.WPF.UI.Appearance;
using ReactiveUI;
using System.ComponentModel;
using System.Windows;
using Splat;

namespace Velopack.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public MainWindow()
    {
        SystemThemeWatcher.Watch(this);
        InitializeComponent();
        this.WhenActivated(d => this.NavigateToView<MainViewModel>());

        Closing += OnClosing;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Resolve MainViewModel from Splat service locator
        var vm = Locator.Current.GetService<MainViewModel>();
        if (vm != null && HasUnsavedChanges(vm))
        {
            var rslt = MessageBox.Show("Save changes before exit?", "Velopack UI", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (rslt == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
            if (rslt == MessageBoxResult.Yes)
            {
                vm.Save();
            }
        }
    }

    private static bool HasUnsavedChanges(MainViewModel vm)
    {
        // If there is no model, nothing to save
        if (vm.Model == null) return false;

        // If project path not chosen yet, treat as dirty
        if (string.IsNullOrWhiteSpace(vm.FilePath)) return true;

        // Minimal heuristic: if the expected project file doesn't exist yet, it's dirty
        try
        {
            var path = vm.FilePath!;
            if (!path.EndsWith(PathFolderHelper.ProjectFileExtension))
            {
                path = System.IO.Path.Combine(path, $"{vm.Model.AppId}{PathFolderHelper.ProjectFileExtension}");
            }
            return !System.IO.File.Exists(path);
        }
        catch
        {
            return true;
        }
    }
}
