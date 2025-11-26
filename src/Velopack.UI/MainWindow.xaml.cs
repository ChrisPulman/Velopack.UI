// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.Versioning;
using System.Windows;
using CrissCross;
using CrissCross.WPF.UI.Appearance;
using ReactiveUI;
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
        InitializeComponent();
        this.WhenActivated(d => this.NavigateToView<MainViewModel>());

        Closing += OnClosing;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        var vm = Locator.Current.GetService<MainViewModel>();
        if (vm != null && vm.HasUnsavedChanges)
        {
            var rslt = MessageBox.Show("Save changes before exit?", "Velopack UI", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (rslt == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (rslt == MessageBoxResult.Yes)
            {
                vm.Save();
            }
        }
    }
}
