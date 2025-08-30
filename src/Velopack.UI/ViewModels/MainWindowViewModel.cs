using System.Runtime.Versioning;
using CrissCross;
using ReactiveUI;
using Splat;
using Velopack.UI.Views;

namespace Velopack.UI;

[SupportedOSPlatform("windows10.0.19041.0")]
public class MainWindowViewModel : RxObject
{
    public MainWindowViewModel()
    {
        Locator.CurrentMutable.RegisterConstant<MainViewModel>(new());
        Locator.CurrentMutable.Register<IViewFor<MainViewModel>>(() => new MainView());
        Locator.CurrentMutable.SetupComplete();
    }
}
