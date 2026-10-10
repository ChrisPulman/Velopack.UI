// <copyright file="WebConnectionEdit.xaml.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System.Diagnostics;
using System.Runtime.Versioning;
using System.Windows;

namespace Velopack.UI;

/// <summary>
/// WebConnectionEdit.
/// </summary>
/// <seealso cref="Window" />
/// <seealso cref="System.Windows.Markup.IComponentConnector" />
/// <seealso cref="System.Windows.Markup.IStyleConnector" />
[SupportedOSPlatform("windows10.0.19041.0")]
public partial class WebConnectionEdit
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebConnectionEdit"/> class.
    /// </summary>
    public WebConnectionEdit() => this.InitializeComponent();

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        this.DialogResult = true;
        this.Close();
    }

    private void GitHubCliSignIn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            GitHubCliReleasePublisher.StartInteractiveSignIn();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Unable to start GitHub CLI sign-in: {ex.Message}", "GitHub CLI", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Hyperlink_Click(object sender, RoutedEventArgs e) =>
        Process.Start("http://docs.aws.amazon.com/awscloudtrail/latest/userguide/cloudtrail-s3-bucket-naming-requirements.html");
}
