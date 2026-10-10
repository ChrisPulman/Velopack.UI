// <copyright file="MainWindow.xaml.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System.ComponentModel;
using System.Runtime.Versioning;
using System.Windows;
using CrissCross;
using CrissCross.WPF.UI.Appearance;
using ReactiveUI;
using ReactiveUI.Primitives.Disposables;
using Splat;

namespace Velopack.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public partial class MainWindow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        SystemThemeWatcher.Watch(this);
        this.InitializeComponent();
        this.WhenActivated((MultipleDisposable _) => this.NavigateToView(typeof(MainViewModel)));

        this.Closing += this.OnClosing;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        var vm = Locator.Current.GetService<MainViewModel>();
        if (vm?.HasUnsavedChanges != true)
        {
            return;
        }

        var rslt = MessageBox.Show("Save changes before exit?", "Velopack UI", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (rslt == MessageBoxResult.Cancel)
        {
            e.Cancel = true;
            return;
        }

        if (rslt != MessageBoxResult.Yes)
        {
            return;
        }

        vm.Save();
    }
}
