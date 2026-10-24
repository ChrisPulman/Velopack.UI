// <copyright file="MainViewTests.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System.Windows.Threading;
using ReactiveUI;
using ReactiveUI.Builder;
using Velopack.UI.Views;

namespace Velopack.UI.Tests;

/// <summary>Verifies the package editor can load its compiled XAML before activation.</summary>
public sealed class MainViewTests
{
    /// <summary>Verifies the editor constructs its package tree, busy overlay, and publishing controls.</summary>
    /// <returns>A task that completes after the assertions.</returns>
    [Test]
    public async Task Constructor_LoadsPackageEditorAndViewModelProperty()
    {
        var completion = new TaskCompletionSource<(bool HasBusyIndicator, bool HasPackageTree, bool HasPublishControls, bool HasViewModelProperty)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                _ = Dispatcher.CurrentDispatcher;
                RxAppBuilder.CreateReactiveUIBuilder().WithWpf().Build();
                var view = new MainView();
                IViewFor untypedView = view;
                untypedView.ViewModel = null;
                completion.SetResult((
                    view.FindName("ShellBusyIndicator") != null,
                    view.FindName("PackageTreeview") != null,
                    view.FindName("PublishPackageOnlyUpdate") != null && view.FindName("PublishPackageComplete") != null,
                    MainView.ViewModelProperty.PropertyType == typeof(MainViewModel) && view.ViewModel == null && untypedView.ViewModel == null));
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));

        await Assert.That(result.HasBusyIndicator).IsTrue();
        await Assert.That(result.HasPackageTree).IsTrue();
        await Assert.That(result.HasPublishControls).IsTrue();
        await Assert.That(result.HasViewModelProperty).IsTrue();
    }
}
