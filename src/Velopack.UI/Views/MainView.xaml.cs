using ReactiveUI;
using Splat;

namespace Velopack.UI.Views;

/// <summary>
/// Interaction logic for MainView.xaml
/// </summary>
public partial class MainView
{
    public MainView()
    {
        InitializeComponent();
        this.WhenActivated(d => DataContext = ViewModel = Locator.Current.GetService<MainViewModel>());
    }
}
