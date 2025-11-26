// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.Versioning;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Splat;

namespace Velopack.UI.Views;

/// <summary>
/// Interaction logic for MainView.xaml.
/// </summary>
[IViewFor<MainViewModel>]
[SupportedOSPlatform("windows10.0.19041.0")]
public partial class MainView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainView"/> class.
    /// </summary>
    public MainView()
    {
        InitializeComponent();
        this.WhenActivated(d => DataContext = ViewModel = Locator.Current.GetService<MainViewModel>()!);
    }
}
