// <copyright file="MainView.xaml.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System.Runtime.Versioning;
using System.Windows;
using ReactiveUI;
using ReactiveUI.Primitives.Disposables;
using Splat;

namespace Velopack.UI.Views;

/// <summary>
/// Interaction logic for MainView.xaml.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public partial class MainView : IViewFor<MainViewModel>
{
    /// <summary>
    /// Identifies the view model dependency property.
    /// </summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(MainViewModel), typeof(MainView));

    /// <summary>
    /// Initializes a new instance of the <see cref="MainView"/> class.
    /// </summary>
    public MainView()
    {
        this.InitializeComponent();
        this.WhenActivated((MultipleDisposable _) => this.DataContext = this.ViewModel = Locator.Current.GetService<MainViewModel>()!);
    }

    /// <summary>
    /// Gets or sets the view model displayed by this view.
    /// </summary>
    public MainViewModel? ViewModel
    {
        get => (MainViewModel?)this.GetValue(ViewModelProperty);
        set => this.SetValue(ViewModelProperty, value);
    }

    /// <inheritdoc/>
    object? IViewFor.ViewModel
    {
        get => this.ViewModel;
        set => this.ViewModel = (MainViewModel?)value;
    }
}
