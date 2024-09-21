using Velopack.UI.Views;
using CrissCross;
using ReactiveUI;
using Splat;

namespace Velopack.UI;

public class MainWindowViewModel : RxObject
{
    public MainWindowViewModel()
    {
        Locator.CurrentMutable.RegisterConstant<MainViewModel>(new());
        Locator.CurrentMutable.Register<IViewFor<MainViewModel>>(() => new MainView());
        Locator.CurrentMutable.SetupComplete();
    }
}
