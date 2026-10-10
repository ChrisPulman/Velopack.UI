// <copyright file="MainWindowViewModel.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

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
