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
        var vm = Locator.Current.GetService<MainViewModel>();
        if (vm != null && vm.HasUnsavedChanges)
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
}
