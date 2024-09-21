using CrissCross;
using CrissCross.WPF.UI.Appearance;
using ReactiveUI;

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
        this.WhenActivated(d =>
        {
            this.NavigateToView<MainViewModel>();
        });
    }
}
