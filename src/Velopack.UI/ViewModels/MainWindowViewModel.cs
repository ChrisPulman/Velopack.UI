// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.Versioning;
using CrissCross;
using ReactiveUI;
using Splat;
using Velopack.UI.Views;

namespace Velopack.UI;

/// <summary>
/// MainWindowViewModel.
/// </summary>
/// <seealso cref="CrissCross.RxObject" />
[SupportedOSPlatform("windows10.0.19041.0")]
public class MainWindowViewModel : RxObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    public MainWindowViewModel()
    {
        Locator.CurrentMutable.RegisterConstant<MainViewModel>(new());
        Locator.CurrentMutable.Register<IViewFor<MainViewModel>>(() => new MainView());
        Locator.CurrentMutable.SetupComplete();
    }
}
