using System.Reactive.Disposables;
using ReactiveMarbles.ObservableEvents;
using CrissCross;
using CrissCross.WPF.UI.Appearance;
using ReactiveUI;
using System.Windows.Forms;

namespace Clowd.Squirrel.UI;

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
            GoBackButton.Command = ReactiveCommand.Create(() => this.NavigateBack(), this.CanNavigateBack()).DisposeWith(d);
            this.NavigateToView<MainViewModel>();            
        });
    }
}
