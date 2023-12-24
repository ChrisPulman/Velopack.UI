using System.Reactive.Disposables;
using System.Windows.Controls;
using CrissCross;
using ReactiveUI;

namespace Clowd.Squirrel.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
        this.WhenActivated(d =>
        {
            GoBackButton!.Command = ReactiveCommand.Create(() => this.NavigateBack(), this.CanNavigateBack()).DisposeWith(d);
            ExitApp!.Command = ReactiveCommand.Create(() => App.Current.Shutdown()).DisposeWith(d);
            this.NavigateToView<MainViewModel>();
        });
    }
    private Button? GoBackButton { get; set; }
    private Button? ExitApp { get; set; }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        GoBackButton = (Button)Template.FindName(nameof(GoBackButton), this);
        ExitApp = (Button)Template.FindName(nameof(ExitApp), this);
    }
}
