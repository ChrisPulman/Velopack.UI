using ReactiveUI;
using Splat;

namespace Clowd.Squirrel.UI.Views;

/// <summary>
/// Interaction logic for MainView.xaml
/// </summary>
public partial class MainView
{
    public MainView()
    {
        InitializeComponent();
        this.WhenActivated(d =>
        {
            ViewModel = Locator.Current.GetService<MainViewModel>();
        });
    }
}
